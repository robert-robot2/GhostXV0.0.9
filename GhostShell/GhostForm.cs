using System;
using System.Drawing;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Forms.Application;

namespace GhostShell
{
    public class GhostForm : Form
    {
        // ---- Render surface ----
        private Panel _renderPanel;

        // ---- Theme state ----
        private Color _menuBgColor = Color.FromArgb(0x26, 0x26, 0x2A);
        private Color _menuTextColor = Color.FromArgb(0xF2, 0xF2, 0xF2);
        private Color _clearColor = Color.Black;

        // ---- Menu builder ----
        private GhostMenuBuilder _menuBuilder;

        public GhostForm()
        {
            InitializeForm();
            ApplyDarkMode();
            BuildUI();
            HookEngineEvents();
        }

        // -------------------------------------------------------
        // Form setup
        // -------------------------------------------------------
        private void InitializeForm()
        {
            Text = "GhostX Shell";
            Size = new Size(1280, 720);
            MinimumSize = new Size(640, 480);
            BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void ApplyDarkMode()
        {
            uint dark = 1;
            Win32.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(uint));
        }

        // -------------------------------------------------------
        // UI construction
        // -------------------------------------------------------
        private void BuildUI()
        {
            // ---- Render panel (DX12 draws here) ----
            _renderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };

            // Prevent WinForms from painting over DX12 output
            typeof(Panel).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, _renderPanel, new object[] { false });

            Controls.Add(_renderPanel);

            // ---- Menu strip ----
            _menuBuilder = new GhostMenuBuilder(_menuBgColor, _menuTextColor);
            _menuBuilder.Build(this);

            // Wire menu callbacks
            _menuBuilder.OnExit += (s, e) => Close();
            _menuBuilder.OnBackgroundColor += OnPickBackgroundColor;
            _menuBuilder.OnWindowColor += OnPickWindowColor;
            _menuBuilder.OnWindowTextColor += OnPickWindowTextColor;
            _menuBuilder.OnMenuBgColor += OnPickMenuBgColor;
            _menuBuilder.OnMenuTextColor += OnPickMenuTextColor;
            _menuBuilder.OnAbout += OnAbout;
            _menuBuilder.OnVersion += OnVersion;
        }

        // -------------------------------------------------------
        // Engine lifecycle hooks
        // -------------------------------------------------------
        private void HookEngineEvents()
        {
            // Init engine once the handle exists
            Load += (s, e) =>
            {
                int result = GhostEngine.GhostEngine_Init(
                    _renderPanel.Handle,
                    (uint)_renderPanel.ClientSize.Width,
                    (uint)_renderPanel.ClientSize.Height);

                if (result != 0)
                {
                    MessageBox.Show("GhostEngine failed to initialize.",
                        "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }

                // Drive the render loop from the message pump idle time
                Application.Idle += OnIdle;
            };

            // Resize the swap chain when the render panel changes size
            _renderPanel.Resize += (s, e) =>
            {
                if (_renderPanel.ClientSize.Width > 0 &&
                    _renderPanel.ClientSize.Height > 0)
                {
                    GhostEngine.GhostEngine_Resize(
                        (uint)_renderPanel.ClientSize.Width,
                        (uint)_renderPanel.ClientSize.Height);
                }
            };

            // Clean shutdown
            FormClosing += (s, e) =>
            {
                Application.Idle -= OnIdle;
                GhostEngine.GhostEngine_Shutdown();
            };
        }

        // -------------------------------------------------------
        // Render loop
        // -------------------------------------------------------
        private void OnIdle(object sender, EventArgs e)
        {
            // Drain all pending Windows messages first, then tick
            while (!Win32.PeekMessageExists())
            {
                GhostEngine.GhostEngine_Tick();
            }
        }

        // -------------------------------------------------------
        // Menu callbacks
        // -------------------------------------------------------
        private void OnPickBackgroundColor(object sender, EventArgs e)
        {
            GhostColorDialog.Pick(this, _clearColor, color =>
            {
                _clearColor = color;
                GhostEngine.GhostEngine_SetClearColor(
                    color.R / 255f,
                    color.G / 255f,
                    color.B / 255f);
            });
        }

        private void OnPickWindowColor(object sender, EventArgs e)
        {
            GhostColorDialog.Pick(this, BackColor, color =>
            {
                BackColor = color;
                uint raw = ToDwmColor(color);
                Win32.DwmSetWindowAttribute(Handle, 35, ref raw, sizeof(uint));
            });
        }

        private void OnPickWindowTextColor(object sender, EventArgs e)
        {
            // Start from white as a sensible default for title bar text
            GhostColorDialog.Pick(this, Color.White, color =>
            {
                uint raw = ToDwmColor(color);
                Win32.DwmSetWindowAttribute(Handle, 36, ref raw, sizeof(uint));
            });
        }

        private void OnPickMenuBgColor(object sender, EventArgs e)
        {
            GhostColorDialog.Pick(this, _menuBgColor, color =>
            {
                _menuBgColor = color;
                _menuBuilder.UpdateColors(_menuBgColor, _menuTextColor);
            });
        }

        private void OnPickMenuTextColor(object sender, EventArgs e)
        {
            GhostColorDialog.Pick(this, _menuTextColor, color =>
            {
                _menuTextColor = color;
                _menuBuilder.UpdateColors(_menuBgColor, _menuTextColor);
            });
        }

        private void OnAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "GhostX Engine\nA lightweight Direct3D 12 engine\nNow with a WinForms shell.",
                "About GhostX", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnVersion(object sender, EventArgs e)
        {
            MessageBox.Show(
                "GhostX v0.0.6\nWinForms shell migration complete.",
                "Version", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------

        // DWM expects BGR (COLORREF) not RGB
        private static uint ToDwmColor(Color c)
            => (uint)(c.R | (c.G << 8) | (c.B << 16));
    }
}