using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StudioBrightnessControl
{
    public partial class SettingsForm : Form
    {
        private TrackBar brightnessTrackBar;
        private Label brightnessLabel;
        private Button applyButton;
        private Button cancelButton;
        private Label previewLabel;
        private uint originalBrightness;
        private uint currentPreviewBrightness;

        private static readonly uint[] BRIGHTNESS_STEPS = { 400, 2400, 4400, 7200, 10000, 15000, 20000, 25000, 30000, 35000, 40000, 45000, 50000, 55000, 60000 };

        public uint SelectedBrightness { get; private set; }

        public SettingsForm(uint currentBrightness)
        {
            this.originalBrightness = currentBrightness;
            this.currentPreviewBrightness = currentBrightness;
            this.SelectedBrightness = currentBrightness;
            InitializeComponent2();
            SetTrackBarPosition(currentBrightness);
            UpdateBrightnessDisplay();
        }

        private void InitializeComponent2()
        {
            this.SuspendLayout();

            // 窗体设置
            this.Text = "亮度设置";
            this.Size = new Size(400, 180);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 亮度标签
            var titleLabel = new Label
            {
                Text = "屏幕亮度",
                Location = new Point(20, 20),
                Size = new Size(100, 20),
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold)
            };
            this.Controls.Add(titleLabel);

            // 预览提示标签
            previewLabel = new Label
            {
                Text = "拖动滑块预览亮度变化",
                Location = new Point(150, 20),
                Size = new Size(200, 20),
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = Color.Gray
            };
            this.Controls.Add(previewLabel);

            // 亮度滑块
            brightnessTrackBar = new TrackBar
            {
                Location = new Point(20, 50),
                Size = new Size(300, 45),
                Minimum = 0,
                Maximum = BRIGHTNESS_STEPS.Length - 1,
                TickFrequency = 1,
                TickStyle = TickStyle.BottomRight
            };
            brightnessTrackBar.Scroll += BrightnessTrackBar_Scroll;
            brightnessTrackBar.MouseUp += BrightnessTrackBar_MouseUp;
            this.Controls.Add(brightnessTrackBar);

            // 亮度值显示
            brightnessLabel = new Label
            {
                Location = new Point(330, 50),
                Size = new Size(50, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei", 9)
            };
            this.Controls.Add(brightnessLabel);

            // 应用按钮
            applyButton = new Button
            {
                Text = "应用",
                Location = new Point(200, 110),
                Size = new Size(80, 30)
            };
            applyButton.Click += ApplyButton_Click;
            this.Controls.Add(applyButton);

            // 取消按钮
            cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(290, 110),
                Size = new Size(80, 30)
            };
            cancelButton.Click += CancelButton_Click;
            this.Controls.Add(cancelButton);

            // 设置当前亮度对应的滑块位置
            SetTrackBarPosition(originalBrightness);

            this.ResumeLayout();
        }

        private void SetTrackBarPosition(uint brightness)
        {
            for (int i = 0; i < BRIGHTNESS_STEPS.Length; i++)
            {
                if (BRIGHTNESS_STEPS[i] >= brightness || i == BRIGHTNESS_STEPS.Length - 1)
                {
                    brightnessTrackBar.Value = i;
                    break;
                }
            }
        }

        private async void BrightnessTrackBar_Scroll(object sender, EventArgs e)
        {
            int index = brightnessTrackBar.Value;
            uint newBrightness = BRIGHTNESS_STEPS[index];
            currentPreviewBrightness = newBrightness;

            UpdateBrightnessDisplay();

            // 实时预览亮度变化（但不保存）
            await HIDHelper.SetBrightnessAsync(newBrightness);
        }

        private void BrightnessTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            // 鼠标释放时更新预览提示
            UpdatePreviewLabel();
        }

        private void UpdateBrightnessDisplay()
        {
            int level = brightnessTrackBar.Value + 1;
            int percentage = (int)((brightnessTrackBar.Value * 100.0) / (BRIGHTNESS_STEPS.Length - 1));
            brightnessLabel.Text = $"{level}/15\n{percentage}%";

            UpdatePreviewLabel();
        }

        private void UpdatePreviewLabel()
        {
            if (currentPreviewBrightness != originalBrightness)
            {
                previewLabel.Text = "预览中... 点击应用保存";
                previewLabel.ForeColor = Color.Orange;
            }
            else
            {
                previewLabel.Text = "拖动滑块预览亮度变化";
                previewLabel.ForeColor = Color.Gray;
            }
        }

        private async void ApplyButton_Click(object sender, EventArgs e)
        {
            applyButton.Enabled = false;
            applyButton.Text = "保存中...";

            // 保存当前预览的亮度
            SelectedBrightness = currentPreviewBrightness;
            originalBrightness = currentPreviewBrightness;

            // 确保亮度已经设置（虽然预览时已经设置过，但这里再次确认）
            int result = await HIDHelper.SetBrightnessAsync(SelectedBrightness);

            if (result == 0)
            {
                UpdatePreviewLabel();
                previewLabel.Text = "设置已保存";
                previewLabel.ForeColor = Color.Green;

                // 短暂显示成功消息后关闭
                await Task.Delay(800);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show($"保存亮度设置失败: {result}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                applyButton.Enabled = true;
                applyButton.Text = "应用";
            }
        }

        private async void CancelButton_Click(object sender, EventArgs e)
        {
            // 取消时恢复原始亮度
            if (currentPreviewBrightness != originalBrightness)
            {
                await HIDHelper.SetBrightnessAsync(originalBrightness);
            }

            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 如果用户直接点击X关闭，也恢复原始亮度
            if (e.CloseReason == CloseReason.UserClosing && currentPreviewBrightness != originalBrightness)
            {
                // 注意：这里不能直接await，所以使用同步方式
                _ = HIDHelper.SetBrightnessAsync(originalBrightness);
            }
            base.OnFormClosing(e);
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {

        }
    }
}