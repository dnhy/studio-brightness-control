using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace StudioBrightnessControl
{
    public static class HIDHelper
    {
        // Apple Studio Display 设备标识
        private const string VidStr = "VID_05AC";
        private const string PidStr = "PID_1114";
        private const string InterfaceStr = "MI_07";
        private const string CollectionStr = "Col";

        private static IntPtr hDeviceObject = IntPtr.Zero;
        private static IntPtr preparsedData = IntPtr.Zero;

        private struct DeviceCapabilities
        {
            public ushort ReportLength;
            public ushort UsagePage;
            public ushort Usage;
            public byte ReportId;
        }

        private static DeviceCapabilities inputCaps;
        private static DeviceCapabilities featureCaps;

        // Windows API 常量
        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_DEVICEINTERFACE = 0x00000010;
        private const int SPDRP_HARDWAREID = 0x00000001;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;

        // Windows API 结构体
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public uint cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            public int IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            public int IsRange;
            public int IsStringRange;
            public int IsDesignatorRange;
            public int IsAbsolute;
            public int HasNull;
            public ushort Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            public ushort Reserved2;
            public ushort Reserved3;
            public ushort Reserved4;
            public ushort Reserved5;
            public ushort Reserved6;
            public uint Units;
            public uint UnitsExp;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
            public ushort Usage;
            public ushort Reserved7;
            public ushort StringIndex;
            public ushort Reserved8;
            public ushort DesignatorIndex;
            public ushort Reserved9;
            public ushort DataIndex;
            public ushort Reserved10;
        }

        // Windows API 函数声明
        [DllImport("hid.dll", SetLastError = true)]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            string enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            IntPtr propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetValueCaps(
            int reportType,
            [Out] HIDP_VALUE_CAPS[] valueCaps,
            ref ushort valueCapsLength,
            IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetUsageValue(
            int reportType,
            ushort usagePage,
            ushort linkCollection,
            ushort usage,
            out uint usageValue,
            IntPtr preparsedData,
            byte[] report,
            uint reportLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_SetUsageValue(
            int reportType,
            ushort usagePage,
            ushort linkCollection,
            ushort usage,
            uint usageValue,
            IntPtr preparsedData,
            byte[] report,
            uint reportLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetInputReport(IntPtr hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetFeature(IntPtr hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetFeature(IntPtr hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

        private const int HIDP_STATUS_SUCCESS = 0x00110000;
        private const int HidP_Input = 0;
        private const int HidP_Output = 1;
        private const int HidP_Feature = 2;

        public static async Task<int> InitAsync()
        {
            return await Task.Run(() =>
            {
                if (hDeviceObject != IntPtr.Zero)
                {
                    Deinit();
                }

                try
                {
                    HidD_GetHidGuid(out Guid hidGuid);
                    Console.WriteLine($"HID Guid: {hidGuid}");

                    IntPtr hDevInfoSet = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                    if (hDevInfoSet == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        Console.WriteLine($"SetupDiGetClassDevs failed with error: {error}");
                        return -2;
                    }

                    try
                    {
                        bool deviceFound = false;

                        for (uint memberIndex = 0; ; memberIndex++)
                        {
                            SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA
                            {
                                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                            };

                            if (!SetupDiEnumDeviceInfo(hDevInfoSet, memberIndex, ref deviceInfoData))
                            {
                                int error = Marshal.GetLastWin32Error();
                                if (error == 259) // ERROR_NO_MORE_ITEMS
                                {
                                    Console.WriteLine("No more devices found");
                                    break;
                                }
                                continue;
                            }

                            // 获取硬件ID
                            IntPtr propertyBuffer = Marshal.AllocHGlobal(1024);
                            try
                            {
                                if (SetupDiGetDeviceRegistryProperty(hDevInfoSet, ref deviceInfoData, SPDRP_HARDWAREID,
                                    out uint propertyType, propertyBuffer, 1024, out uint requiredSize))
                                {
                                    string hardwareId = Marshal.PtrToStringAuto(propertyBuffer);
                                    Console.WriteLine($"Checking device: {hardwareId}");

                                    // 检查设备是否匹配
                                    if (hardwareId != null &&
                                        hardwareId.Contains(VidStr) &&
                                        hardwareId.Contains(PidStr) &&
                                        hardwareId.Contains(InterfaceStr) &&
                                        !hardwareId.Contains(CollectionStr))
                                    {
                                        Console.WriteLine("Found matching Apple Studio Display!");
                                        deviceFound = true;

                                        // 获取设备接口
                                        SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA
                                        {
                                            cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                                        };

                                        if (SetupDiEnumDeviceInterfaces(hDevInfoSet, IntPtr.Zero, ref hidGuid, memberIndex, ref deviceInterfaceData))
                                        {
                                            // 获取设备路径
                                            uint requiredSize2;
                                            SetupDiGetDeviceInterfaceDetail(hDevInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out requiredSize2, IntPtr.Zero);

                                            if (requiredSize2 > 0)
                                            {
                                                IntPtr detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize2);
                                                try
                                                {
                                                    // 设置正确的结构体大小
                                                    Marshal.WriteInt32(detailDataBuffer, IntPtr.Size == 8 ? 8 : 6);

                                                    if (SetupDiGetDeviceInterfaceDetail(hDevInfoSet, ref deviceInterfaceData, detailDataBuffer,
                                                        requiredSize2, out requiredSize2, IntPtr.Zero))
                                                    {
                                                        // 读取设备路径
                                                        string devicePath = Marshal.PtrToStringAuto(detailDataBuffer + 4);
                                                        Console.WriteLine($"Device path: {devicePath}");

                                                        // 尝试打开设备
                                                        hDeviceObject = CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE,
                                                            FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                                                        if (hDeviceObject != IntPtr.Zero && hDeviceObject != new IntPtr(-1))
                                                        {
                                                            Console.WriteLine("Device opened successfully!");

                                                            // 获取 preparsed data
                                                            if (HidD_GetPreparsedData(hDeviceObject, out preparsedData))
                                                            {
                                                                // 获取设备能力
                                                                if (HidP_GetCaps(preparsedData, out HIDP_CAPS caps) == HIDP_STATUS_SUCCESS)
                                                                {
                                                                    Console.WriteLine($"Device capabilities:");
                                                                    Console.WriteLine($"  InputReportByteLength: {caps.InputReportByteLength}");
                                                                    Console.WriteLine($"  FeatureReportByteLength: {caps.FeatureReportByteLength}");
                                                                    Console.WriteLine($"  UsagePage: {caps.UsagePage}");
                                                                    Console.WriteLine($"  Usage: {caps.Usage}");

                                                                    // 获取所有值能力并找到正确的 Usage
                                                                    ushort valueCapsLength = 20;
                                                                    HIDP_VALUE_CAPS[] valueCaps = new HIDP_VALUE_CAPS[20];

                                                                    // 查找特征报告的值能力
                                                                    valueCapsLength = 20;
                                                                    int status = HidP_GetValueCaps(HidP_Feature, valueCaps, ref valueCapsLength, preparsedData);
                                                                    if (status == HIDP_STATUS_SUCCESS && valueCapsLength > 0)
                                                                    {
                                                                        Console.WriteLine($"Found {valueCapsLength} feature value capabilities:");

                                                                        for (int i = 0; i < valueCapsLength; i++)
                                                                        {
                                                                            Console.WriteLine($"  [{i}] ReportID: {valueCaps[i].ReportID}, " +
                                                                                              $"UsagePage: {valueCaps[i].UsagePage}, " +
                                                                                              $"Usage: {valueCaps[i].Usage}, " +
                                                                                              $"LogicalMin: {valueCaps[i].LogicalMin}, " +
                                                                                              $"LogicalMax: {valueCaps[i].LogicalMax}");
                                                                        }

                                                                        // 尝试使用第一个特征值能力
                                                                        featureCaps.ReportId = valueCaps[0].ReportID;
                                                                        featureCaps.UsagePage = valueCaps[0].UsagePage;
                                                                        featureCaps.Usage = valueCaps[0].Usage;
                                                                        featureCaps.ReportLength = caps.FeatureReportByteLength;

                                                                        Console.WriteLine($"Using feature - ReportID: {featureCaps.ReportId}, " +
                                                                                          $"UsagePage: {featureCaps.UsagePage}, " +
                                                                                          $"Usage: {featureCaps.Usage}");
                                                                    }
                                                                    else
                                                                    {
                                                                        Console.WriteLine("No feature value capabilities found, trying input report...");

                                                                        // 如果没有特征报告，尝试输入报告
                                                                        valueCapsLength = 20;
                                                                        status = HidP_GetValueCaps(HidP_Input, valueCaps, ref valueCapsLength, preparsedData);
                                                                        if (status == HIDP_STATUS_SUCCESS && valueCapsLength > 0)
                                                                        {
                                                                            Console.WriteLine($"Found {valueCapsLength} input value capabilities:");

                                                                            for (int i = 0; i < valueCapsLength; i++)
                                                                            {
                                                                                Console.WriteLine($"  [{i}] ReportID: {valueCaps[i].ReportID}, " +
                                                                                                  $"UsagePage: {valueCaps[i].UsagePage}, " +
                                                                                                  $"Usage: {valueCaps[i].Usage}");
                                                                            }

                                                                            inputCaps.ReportId = valueCaps[0].ReportID;
                                                                            inputCaps.UsagePage = valueCaps[0].UsagePage;
                                                                            inputCaps.Usage = valueCaps[0].Usage;
                                                                            inputCaps.ReportLength = caps.InputReportByteLength;

                                                                            // 如果没有特征报告，使用输入报告来设置亮度
                                                                            featureCaps = inputCaps;

                                                                            Console.WriteLine($"Using input for feature - ReportID: {featureCaps.ReportId}, " +
                                                                                              $"UsagePage: {featureCaps.UsagePage}, " +
                                                                                              $"Usage: {featureCaps.Usage}");
                                                                        }
                                                                    }

                                                                    return 0; // 成功
                                                                }
                                                                else
                                                                {
                                                                    Console.WriteLine("HidP_GetCaps failed");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Console.WriteLine("HidD_GetPreparsedData failed");
                                                            }

                                                            // 如果失败，清理资源
                                                            Deinit();
                                                        }
                                                        else
                                                        {
                                                            int error = Marshal.GetLastWin32Error();
                                                            Console.WriteLine($"CreateFile failed with error: {error}");
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    Marshal.FreeHGlobal(detailDataBuffer);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(propertyBuffer);
                            }
                        }

                        if (!deviceFound)
                        {
                            Console.WriteLine("No matching Apple Studio Display found");
                            return -11;
                        }

                        return -12; // 设备找到但初始化失败
                    }
                    finally
                    {
                        SetupDiDestroyDeviceInfoList(hDevInfoSet);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HID Init exception: {ex}");
                    return -13;
                }
            });
        }

        public static async Task<(int result, uint value)> GetBrightnessAsync()
        {
            return await Task.Run(() =>
            {
                uint val = 0;

                if (hDeviceObject == IntPtr.Zero)
                    return (-1, val);

                try
                {
                    byte[] dataBuf = new byte[100];
                    dataBuf[0] = inputCaps.ReportId;

                    if (!HidD_GetInputReport(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        return (-2, val);

                    int status = HidP_GetUsageValue(HidP_Input, inputCaps.UsagePage, 0, inputCaps.Usage,
                        out val, preparsedData, dataBuf, inputCaps.ReportLength);

                    if (status != HIDP_STATUS_SUCCESS)
                        return (-3, val);

                    return (0, val);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetBrightness failed: {ex.Message}");
                    return (-4, val);
                }
            });
        }

        public static async Task<int> SetBrightnessAsync(uint val)
        {
            return await Task.Run(() =>
            {
                if (hDeviceObject == IntPtr.Zero)
                {
                    Console.WriteLine("SetBrightness failed: hDeviceObject is null");
                    return -1;
                }

                try
                {
                    Console.WriteLine($"Setting brightness to: {val}");
                    Console.WriteLine($"Using - ReportID: {featureCaps.ReportId}, UsagePage: {featureCaps.UsagePage}, Usage: {featureCaps.Usage}");

                    // 创建报告缓冲区
                    byte[] dataBuf = new byte[featureCaps.ReportLength + 1];
                    Array.Clear(dataBuf, 0, dataBuf.Length);
                    dataBuf[0] = featureCaps.ReportId;

                    Console.WriteLine($"Report length: {featureCaps.ReportLength}, Buffer size: {dataBuf.Length}");

                    // 方法1: 使用 HidP_SetUsageValue
                    int status = HidP_SetUsageValue(HidP_Feature, featureCaps.UsagePage, 0, featureCaps.Usage,
                        val, preparsedData, dataBuf, (uint)dataBuf.Length);

                    Console.WriteLine($"HidP_SetUsageValue status: {status}");

                    if (status == HIDP_STATUS_SUCCESS)
                    {
                        Console.WriteLine("HidP_SetUsageValue succeeded, sending feature report...");
                        if (HidD_SetFeature(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        {
                            Console.WriteLine("Brightness set successfully using HidP_SetUsageValue");
                            return 0;
                        }
                        else
                        {
                            int error = Marshal.GetLastWin32Error();
                            Console.WriteLine($"HidD_SetFeature failed with error: {error}");
                        }
                    }

                    // 方法2: 直接设置报告数据（基于 Apple Studio Display 的实际协议）
                    Console.WriteLine("Trying direct buffer manipulation...");

                    // 重置缓冲区
                    Array.Clear(dataBuf, 0, dataBuf.Length);
                    dataBuf[0] = featureCaps.ReportId;

                    // Apple Studio Display 亮度控制可能需要特定的报告格式
                    // 尝试不同的数据格式和位置

                    // 格式1: 小端序 32位值
                    byte[] valueBytes = BitConverter.GetBytes(val);
                    if (BitConverter.IsLittleEndian)
                    {
                        // 保持小端序
                    }

                    Console.WriteLine($"Value bytes: {BitConverter.ToString(valueBytes)}");

                    // 尝试不同的位置和格式
                    bool[] triedPositions = new bool[dataBuf.Length];

                    // 位置1: 字节1-4 (常见位置)
                    if (dataBuf.Length >= 5)
                    {
                        Array.Copy(valueBytes, 0, dataBuf, 1, 4);
                        Console.WriteLine("Trying position 1-4 (little endian)");
                        if (HidD_SetFeature(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        {
                            Console.WriteLine("Success with position 1-4 (little endian)");
                            return 0;
                        }
                        triedPositions[1] = true;
                    }

                    // 位置2: 字节1-4 大端序
                    if (dataBuf.Length >= 5)
                    {
                        Array.Clear(dataBuf, 1, dataBuf.Length - 1);
                        byte[] bigEndianBytes = new byte[4];
                        bigEndianBytes[0] = valueBytes[3];
                        bigEndianBytes[1] = valueBytes[2];
                        bigEndianBytes[2] = valueBytes[1];
                        bigEndianBytes[3] = valueBytes[0];
                        Array.Copy(bigEndianBytes, 0, dataBuf, 1, 4);
                        Console.WriteLine("Trying position 1-4 (big endian)");
                        if (HidD_SetFeature(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        {
                            Console.WriteLine("Success with position 1-4 (big endian)");
                            return 0;
                        }
                        triedPositions[1] = true;
                    }

                    // 位置3: 字节2-5
                    if (dataBuf.Length >= 6)
                    {
                        Array.Clear(dataBuf, 1, dataBuf.Length - 1);
                        Array.Copy(valueBytes, 0, dataBuf, 2, 4);
                        Console.WriteLine("Trying position 2-5 (little endian)");
                        if (HidD_SetFeature(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        {
                            Console.WriteLine("Success with position 2-5 (little endian)");
                            return 0;
                        }
                        triedPositions[2] = true;
                    }

                    // 位置4: 16位值（可能只需要2字节）
                    if (dataBuf.Length >= 3)
                    {
                        Array.Clear(dataBuf, 1, dataBuf.Length - 1);
                        ushort shortVal = (ushort)(val & 0xFFFF);
                        byte[] shortBytes = BitConverter.GetBytes(shortVal);
                        Array.Copy(shortBytes, 0, dataBuf, 1, 2);
                        Console.WriteLine($"Trying 16-bit value: {shortVal} at position 1-2");
                        if (HidD_SetFeature(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        {
                            Console.WriteLine("Success with 16-bit value");
                            return 0;
                        }
                        triedPositions[1] = true;
                    }

                    // 位置5: 单个字节（0-255 映射）
                    if (dataBuf.Length >= 2)
                    {
                        Array.Clear(dataBuf, 1, dataBuf.Length - 1);
                        byte byteVal = (byte)((val * 255) / 60000); // 映射到 0-255
                        dataBuf[1] = byteVal;
                        Console.WriteLine($"Trying 8-bit value: {byteVal} at position 1");
                        if (HidD_SetFeature(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                        {
                            Console.WriteLine("Success with 8-bit value");
                            return 0;
                        }
                        triedPositions[1] = true;
                    }

                    // 方法3: 尝试输出报告而不是特征报告
                    Console.WriteLine("Trying output report instead of feature report...");
                    Array.Clear(dataBuf, 0, dataBuf.Length);
                    dataBuf[0] = featureCaps.ReportId;
                    Array.Copy(valueBytes, 0, dataBuf, 1, 4);

                    if (HidD_SetOutputReport(hDeviceObject, dataBuf, (uint)dataBuf.Length))
                    {
                        Console.WriteLine("Success with output report");
                        return 0;
                    }

                    Console.WriteLine("All methods failed");
                    return -4;

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SetBrightness exception: {ex}");
                    return -5;
                }
            });
        }

        // 添加输出报告的函数声明
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetOutputReport(IntPtr hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);
        public static void Deinit()
        {
            try
            {
                if (preparsedData != IntPtr.Zero)
                {
                    HidD_FreePreparsedData(preparsedData);
                    preparsedData = IntPtr.Zero;
                }

                if (hDeviceObject != IntPtr.Zero && hDeviceObject != new IntPtr(-1))
                {
                    CloseHandle(hDeviceObject);
                    hDeviceObject = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deinit failed: {ex.Message}");
            }
        }
    }
}