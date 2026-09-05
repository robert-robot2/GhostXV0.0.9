using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GhostShell
{
    // ================================================================
    //  EwiSystemState
    //  All values match Phase 24 spec exactly.
    //  When STM32 arrives: replace SimTick() body with serial port reads.
    //  Every property maps 1:1 to a physical sensor or actuator.
    // ================================================================
    public class EwiSystemState
    {
        // ---- Water Tank (10 gallon poly, unpressurized, vented) ----
        public float TankCapacityLitres { get; set; } = 37.854f;   // 10 US gal
        public float TankLevelLitres { get; set; } = 37.854f;    // current fill
        public float TankLevelPct => (TankLevelLitres / TankCapacityLitres) * 100f;
        public bool TankLevelLow => TankLevelPct < 15f;
        public bool TankLevelCritical => TankLevelPct < 5f;
        public bool TankOk => TankLevelPct > 5f;

        // ---- Ball Valve (manual 1/2" shutoff) ----
        public bool BallValveOpen { get; set; } = true;

        // ---- 24V Diaphragm Feed Pump (15 L/min max) ----
        public bool PumpRunning { get; set; } = true;
        public float PumpFlowLpm { get; set; } = 0f;    // L/min actual
        public float PumpSupplyPsi { get; set; } = 0f;    // low-pressure supply side

        // ---- Dual Stage Filtration ----
        public bool Filter50umOk { get; set; } = true;
        public bool Filter5umOk { get; set; } = true;
        public float FilterDeltaP { get; set; } = 0f;    // PSI across both stages
        public bool FilterClogged => FilterDeltaP > 8f;  // service threshold

        // ---- EWI High Pressure System ----
        public bool EwiPumpRunning { get; set; } = false;
        public float EwiPumpPsi { get; set; } = 0f;    // 0–300 PSI
        public float EwiAccumulatorPsi { get; set; } = 0f;    // 0.5L bladder accumulator
        public bool EwiSolenoidOpen { get; set; } = false; // injector solenoid state
        public float InjectionPulseMs { get; set; } = 0f;    // ms per pulse
        public float EwiFlowRateMlpm { get; set; } = 0f;    // ml/min into boiler
        public bool CheckValveOk { get; set; } = true;
        public bool NozzleOk { get; set; } = true;

        // ---- Aegis Boiler Mark 1 ----
        public float TargetPsi { get; set; } = 90f;   // 90–500 PSI
        public float LivePsi { get; set; } = 0f;    // live boiler pressure
        public float BowlTempC { get; set; } = 20f;   // copper bowl thermocouple
        public float VesselTempC { get; set; } = 20f;   // SS304 shell thermocouple
        public bool HeaterOn { get; set; } = false;
        public float HeaterWatts { get; set; } = 0f;    // 0–3000W
        public bool PrvOpen { get; set; } = false; // PRV triggered >120 PSI
        public bool FusiblePlugOk { get; set; } = true;

        // ---- PID / Safety ----
        public bool PidCutoff { get; set; } = false; // EWI halted >115 PSI
        public bool PidActive { get; set; } = false;
        public float ExhaustPsiA { get; set; } = 0f;
        public float ExhaustPsiB { get; set; } = 0f;

        // ---- Flow Test Session ----
        public bool FlowTestRunning { get; set; } = false;
        public float FlowTestElapsedS { get; set; } = 0f;
        public float TotalWaterUsedLitres { get; set; } = 0f;
        public float WaterConsumptionRateLph { get; set; } = 0f; // L/hr

        // ---- Alarms ----
        public List<string> ActiveAlarms { get; set; } = new List<string>();
    }

    // ================================================================
    //  GhostSteamPanel — Full EWI Water System Monitor
    //  Phase 24 · Aegis Boiler Mark 1
    // ================================================================
    public class GhostSteamPanel : Panel
    {
        // ---- State ----
        private EwiSystemState _state = new EwiSystemState();
        private float _throttleNorm = 0f;

        // ---- Timers ----
        private System.Windows.Forms.Timer _simTimer;
        private float _flowPhase = 0f;
        private float _simDt = 0.033f; // 33ms = ~30fps

        // ---- Layout ----
        private const int Pad = 16;
        private const int NodeW = 96;
        private const int NodeH = 68;
        private const int NodeGap = 50;
        private const int GaugeSize = 200;
        private const int ThrottleW = 48;

        // ---- Colours ----
        private static readonly Color ColBg = Color.FromArgb(210, 10, 13, 18);
        private static readonly Color ColBorder = Color.FromArgb(45, 58, 70);
        private static readonly Color ColGreen = Color.FromArgb(57, 255, 20);
        private static readonly Color ColAmber = Color.FromArgb(255, 180, 0);
        private static readonly Color ColRed = Color.FromArgb(255, 60, 60);
        private static readonly Color ColBlue = Color.FromArgb(0, 180, 255);
        private static readonly Color ColCyan = Color.FromArgb(0, 229, 255);
        private static readonly Color ColMagenta = Color.FromArgb(255, 0, 200);
        private static readonly Color ColText = Color.FromArgb(220, 228, 235);
        private static readonly Color ColMuted = Color.FromArgb(85, 105, 120);
        private static readonly Color ColFlow = Color.FromArgb(0, 200, 255);
        private static readonly Color ColWater = Color.FromArgb(64, 224, 208);
        private static readonly Color ColBowl = Color.FromArgb(184, 115, 51);
        private static readonly Color ColVessel = Color.FromArgb(192, 160, 96);

        // ---- Fonts ----
        private static readonly Font FntLabel = new Font("Courier New", 7.5f, FontStyle.Bold);
        private static readonly Font FntValue = new Font("Courier New", 8.5f);
        private static readonly Font FntTitle = new Font("Courier New", 9f, FontStyle.Bold);
        private static readonly Font FntBig = new Font("Courier New", 20f, FontStyle.Bold);
        private static readonly Font FntSub = new Font("Courier New", 7.5f);
        private static readonly Font FntAlarm = new Font("Courier New", 7f, FontStyle.Bold);
        private static readonly Font FntBtn = new Font("Courier New", 8f, FontStyle.Bold);

        // ---- Throttle drag ----
        private bool _dragging = false;
        private Rectangle _throttleTrackRect;

        // ---- Buttons ----
        private Rectangle _btnRefill;
        private Rectangle _btnFlowTest;
        private Rectangle _btnHeater;
        private Rectangle _btnEwi;
        private Rectangle _btnPid;

        public GhostSteamPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;

            _simTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _simTimer.Tick += SimTick;
            _simTimer.Start();

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
        }

        // ================================================================
        //  HARDWARE ABSTRACTION LAYER
        //  Replace this method body with STM32 serial reads.
        //  All outputs write directly to _state properties.
        //  Format from STM32: JSON or CSV line per 33ms tick.
        // ================================================================
        private void SimTick(object sender, EventArgs e)
        {
            _flowPhase = (_flowPhase + 0.045f) % 1f;
            _state.ActiveAlarms.Clear();

            float dt = _simDt;

            // ---- Throttle → target PSI ----
            _state.TargetPsi = 90f + _throttleNorm * 410f;

            // ---- Heater logic ----
            if (_state.HeaterOn && _state.TankOk)
            {
                // Heat ramps bowl and vessel toward operating temps
                float heatRate = (_state.HeaterWatts / 3000f) * 8f * dt;
                _state.BowlTempC = Math.Min(_state.BowlTempC + heatRate * 2.5f, 450f);
                _state.VesselTempC = Math.Min(_state.VesselTempC + heatRate * 0.8f, 220f);
            }
            else
            {
                // Cool down slowly
                _state.BowlTempC = Math.Max(20f, _state.BowlTempC - 0.4f * dt);
                _state.VesselTempC = Math.Max(20f, _state.VesselTempC - 0.2f * dt);
            }

            // ---- Feed pump ----
            if (_state.PumpRunning && _state.BallValveOpen && _state.TankOk)
            {
                _state.PumpFlowLpm = 12f + (_throttleNorm * 3f); // 12–15 L/min
                _state.PumpSupplyPsi = 8f + (_throttleNorm * 6f); // 8–14 PSI supply
            }
            else
            {
                _state.PumpFlowLpm = 0f;
                _state.PumpSupplyPsi = 0f;
            }

            // ---- Filter ΔP (climbs as flow increases, alarm >8 PSI) ----
            if (_state.PumpRunning)
                _state.FilterDeltaP = 1.5f + (_throttleNorm * 2.5f);
            else
                _state.FilterDeltaP = 0f;

            // ---- EWI system ----
            bool ewiCanRun = _state.EwiPumpRunning
                          && _state.Filter50umOk
                          && _state.Filter5umOk
                          && _state.CheckValveOk
                          && _state.NozzleOk
                          && _state.TankOk
                          && !_state.PidCutoff;

            if (ewiCanRun)
            {
                _state.EwiPumpPsi = 100f + _throttleNorm * 200f; // 100–300 PSI
                _state.EwiAccumulatorPsi = _state.EwiPumpPsi - 5f;
                _state.EwiSolenoidOpen = true;
                _state.InjectionPulseMs = 1.5f + _throttleNorm * 8f;  // 1.5–9.5 ms
                _state.EwiFlowRateMlpm = 80f + _throttleNorm * 320f; // 80–400 ml/min
            }
            else
            {
                _state.EwiPumpPsi = 0f;
                _state.EwiAccumulatorPsi = 0f;
                _state.EwiSolenoidOpen = false;
                _state.InjectionPulseMs = 0f;
                _state.EwiFlowRateMlpm = 0f;
            }

            // ---- Boiler pressure simulation ----
            float psiTarget = ewiCanRun && _state.BowlTempC > 160f
                              ? _state.TargetPsi : 0f;
            float psiDiff = psiTarget - _state.LivePsi;
            _state.LivePsi = Math.Max(0f, _state.LivePsi + psiDiff * 0.04f);

            // ---- PRV ----
            _state.PrvOpen = _state.LivePsi >= 120f;
            if (_state.PrvOpen)
                _state.LivePsi = Math.Max(_state.LivePsi - 5f * dt, 118f);

            // ---- PID cutoff >115 PSI ----
            _state.PidCutoff = _state.PidActive && _state.LivePsi >= 115f;

            // ---- Exhaust PSI (post piston, PCW exhaust valves) ----
            _state.ExhaustPsiA = _state.LivePsi > 0 ? _state.LivePsi * 0.08f : 0f;
            _state.ExhaustPsiB = _state.LivePsi > 0 ? _state.LivePsi * 0.07f : 0f;

            // ---- Water consumption ----
            if (ewiCanRun && _state.EwiFlowRateMlpm > 0)
            {
                float litresPerSec = (_state.EwiFlowRateMlpm / 1000f) / 60f;
                _state.TankLevelLitres = Math.Max(0f, _state.TankLevelLitres - litresPerSec * dt);
                _state.TotalWaterUsedLitres += litresPerSec * dt;
                _state.WaterConsumptionRateLph = _state.EwiFlowRateMlpm / 1000f * 60f;
            }
            else
            {
                _state.WaterConsumptionRateLph = 0f;
            }

            // ---- Flow test timer ----
            if (_state.FlowTestRunning)
                _state.FlowTestElapsedS += dt;

            // ---- Alarms ----
            if (_state.TankLevelCritical) _state.ActiveAlarms.Add("TANK CRITICAL < 5%");
            else if (_state.TankLevelLow) _state.ActiveAlarms.Add("TANK LOW < 15%");
            if (_state.FilterClogged) _state.ActiveAlarms.Add("FILTER CLOGGED — ΔP > 8 PSI");
            if (_state.PrvOpen) _state.ActiveAlarms.Add("PRV OPEN — BOILER > 120 PSI");
            if (_state.PidCutoff) _state.ActiveAlarms.Add("PID CUTOFF — EWI HALTED > 115 PSI");
            if (_state.BowlTempC > 400f) _state.ActiveAlarms.Add("BOWL TEMP CRITICAL > 400°C");
            if (_state.VesselTempC > 200f) _state.ActiveAlarms.Add("VESSEL TEMP HIGH > 200°C");
            if (!_state.CheckValveOk) _state.ActiveAlarms.Add("CHECK VALVE FAULT");
            if (!_state.NozzleOk) _state.ActiveAlarms.Add("NOZZLE FAULT");
            if (_state.TankLevelLitres == 0) _state.ActiveAlarms.Add("TANK EMPTY — PUMP DRY RUN RISK");

            Invalidate();
        }

        // ================================================================
        //  PAINT
        // ================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int pw = Width - Pad * 2;
            int ph = Height - Pad * 2;

            // Background
            using var bgBrush = new SolidBrush(ColBg);
            using var borderPen = new Pen(ColBorder, 1.5f);
            g.FillRoundedRect(bgBrush, Pad, Pad, pw, ph, 12);
            g.DrawRoundedRect(borderPen, Pad, Pad, pw, ph, 12);

            // Layout regions
            int titleH = 36;
            int flowY = Pad + titleH + 10;
            int flowH = NodeH + 16;
            int midY = flowY + flowH + 8;
            int gaugeX = Width - GaugeSize - Pad - 60 - ThrottleW - 12;
            int gaugeY = flowY;
            int throttleX = Width - ThrottleW - Pad - 12;
            int throttleY = flowY;
            int throttleH = Height - throttleY - Pad - 80;
            int alarmY = Height - Pad - 72;
            int statusY = alarmY - 24;
            int btnY = midY;
            int infoW = gaugeX - Pad - 24;

            DrawTitle(g, Pad + 14, Pad + 8);
            DrawFlowRow(g, Pad + 20, flowY + 8);
            DrawBoilerBlock(g, Pad + 20, midY, infoW);
            DrawThermalPanel(g, Pad + 20, midY + 86, infoW);
            DrawFlowTestPanel(g, Pad + 20, midY + 168, infoW);
            DrawButtons(g, Pad + 20, midY + 268, infoW);
            DrawWaterTankGauge(g, gaugeX - 10, gaugeY, GaugeSize - 10);
            DrawPressureGauge(g, gaugeX + GaugeSize - 8, gaugeY, GaugeSize);
            DrawThrottle(g, throttleX, throttleY, ThrottleW, throttleH);
            DrawAlarmPanel(g, Pad + 14, alarmY, pw - 28);
            DrawStatusBar(g, Pad + 14, statusY, pw - 28);
        }

        // ----------------------------------------------------------------
        //  Title bar
        // ----------------------------------------------------------------
        private void DrawTitle(Graphics g, int x, int y)
        {
            using var accent = new Pen(ColVessel, 2f);
            g.DrawLine(accent, x, y + 20, x + Width - Pad * 2 - 28, y + 20);

            string title = "◈  AEGIS BOILER MARK 1  ·  EWI WATER SYSTEM  ·  PHASE 24  ·  10 GAL POLY TANK  ·  300 PSI EWI";
            using var tb = new SolidBrush(ColText);
            g.DrawString(title, FntTitle, tb, x, y);

            string badge = _state.PidCutoff ? "⚠ PID CUTOFF"
                         : _state.FlowTestRunning ? "● FLOW TEST RUNNING"
                         : "● STANDBY";
            Color bc = _state.PidCutoff ? ColRed
                     : _state.FlowTestRunning ? ColGreen : ColMuted;
            using var bb = new SolidBrush(bc);
            g.DrawString(badge, FntLabel, bb, x + Width - Pad * 2 - 180, y + 2);
        }

        // ----------------------------------------------------------------
        //  Flow row — 8 nodes with animated flow dots
        // ----------------------------------------------------------------
        private void DrawFlowRow(Graphics g, int startX, int startY)
        {
            var nodes = BuildNodes();
            int rightEdge = Width - GaugeSize * 2 - Pad - 80 - ThrottleW;
            int nodeStep = NodeW + NodeGap;
            int count = Math.Min(nodes.Length, (rightEdge - startX) / nodeStep);
            int x = startX;

            for (int i = 0; i < count; i++)
            {
                var (label, value, value2, status, icon) = nodes[i];
                DrawNode(g, x, startY, label, value, value2, status, icon);
                if (i < count - 1)
                    DrawFlowArrow(g, x + NodeW, startY + NodeH / 2, NodeGap, _flowPhase,
                                  _state.PumpRunning && _state.BallValveOpen);
                x += nodeStep;
            }
        }

        private (string label, string val1, string val2, Color status, string icon)[] BuildNodes()
        {
            Color tankCol = _state.TankLevelCritical ? ColRed
                          : _state.TankLevelLow ? ColAmber : ColWater;

            Color valveCol = _state.BallValveOpen ? ColGreen : ColRed;
            Color pumpCol = _state.PumpRunning ? ColGreen : ColRed;
            Color f50Col = _state.Filter50umOk ? ColGreen : ColRed;
            Color f5Col = _state.Filter5umOk ? ColGreen : ColRed;
            Color ewiCol = _state.EwiPumpRunning ? ColGreen : ColMuted;
            Color checkCol = _state.CheckValveOk ? ColGreen : ColRed;
            Color nozzleCol = _state.NozzleOk ? ColCyan : ColRed;

            return new[]
            {
                ("10 GAL TANK",
                    $"{_state.TankLevelLitres:F1}L",
                    $"{_state.TankLevelPct:F0}%",
                    tankCol, "💧"),
                ("BALL VALVE",
                    _state.BallValveOpen ? "OPEN" : "CLOSED",
                    "1/2\" MANUAL",
                    valveCol, "🔧"),
                ("24V PUMP",
                    $"{_state.PumpFlowLpm:F1} L/m",
                    $"{_state.PumpSupplyPsi:F0} PSI",
                    pumpCol, "⚙"),
                ("50µm FILTER",
                    _state.Filter50umOk ? "OK" : "CLOGGED",
                    $"ΔP {_state.FilterDeltaP:F1}",
                    f50Col, "▣"),
                ("5µm FILTER",
                    _state.Filter5umOk ? "OK" : "CLOGGED",
                    "FINE",
                    f5Col, "▣"),
                ("EWI 300PSI",
                    $"{_state.EwiPumpPsi:F0} PSI",
                    $"ACC {_state.EwiAccumulatorPsi:F0}",
                    ewiCol, "⚡"),
                ("CHECK VALVE",
                    _state.CheckValveOk ? "CLEAR" : "FAULT",
                    "NOZZLE GUARD",
                    checkCol, "◈"),
                ("NOZZLE",
                    $"{_state.InjectionPulseMs:F1}ms",
                    $"{_state.EwiFlowRateMlpm:F0} ml/m",
                    nozzleCol, "≋"),
            };
        }

        private void DrawNode(Graphics g, int x, int y,
            string label, string val1, string val2, Color status, string icon)
        {
            using var bg = new SolidBrush(Color.FromArgb(28, 38, 50));
            using var border = new Pen(status, 1.5f);
            g.FillRoundedRect(bg, x, y, NodeW, NodeH, 7);
            g.DrawRoundedRect(border, x, y, NodeW, NodeH, 7);

            using var dotBrush = new SolidBrush(status);
            g.FillEllipse(dotBrush, x + NodeW - 13, y + 6, 8, 8);

            using var iconBrush = new SolidBrush(status);
            g.DrawString(icon, FntLabel, iconBrush, x + 5, y + 5);

            using var lblBrush = new SolidBrush(ColMuted);
            g.DrawString(label, FntLabel, lblBrush, x + 4, y + NodeH - 42);

            using var v1Brush = new SolidBrush(ColText);
            g.DrawString(val1, FntValue, v1Brush, x + 4, y + NodeH - 28);

            using var v2Brush = new SolidBrush(status);
            g.DrawString(val2, FntAlarm, v2Brush, x + 4, y + NodeH - 14);
        }

        private void DrawFlowArrow(Graphics g, int x, int midY, int length, float phase, bool flowing)
        {
            using var linePen = new Pen(ColBorder, 1f);
            g.DrawLine(linePen, x, midY, x + length, midY);

            Color arrowCol = flowing ? ColFlow : ColMuted;
            using var ap = new Pen(arrowCol, 1.5f);
            int ax = x + length - 2;
            g.DrawLine(ap, ax - 6, midY - 4, ax, midY);
            g.DrawLine(ap, ax - 6, midY + 4, ax, midY);

            if (flowing)
            {
                using var db = new SolidBrush(ColFlow);
                for (int d = 0; d < 3; d++)
                {
                    float t = (phase + d / 3f) % 1f;
                    int dotX = x + (int)(t * (length - 8));
                    g.FillEllipse(db, dotX, midY - 2, 4, 4);
                }
            }
        }

        // ----------------------------------------------------------------
        //  Boiler live block
        // ----------------------------------------------------------------
        private void DrawBoilerBlock(Graphics g, int x, int y, int w)
        {
            int h = 78;
            Color bc = _state.LivePsi < 90f ? ColGreen
                     : _state.LivePsi < 115f ? ColAmber : ColRed;

            using var bg = new SolidBrush(Color.FromArgb(22, 30, 18));
            using var border = new Pen(bc, 2f);
            g.FillRoundedRect(bg, x, y, w, h, 8);
            g.DrawRoundedRect(border, x, y, w, h, 8);

            using var lblBrush = new SolidBrush(ColVessel);
            g.DrawString("🛡  AEGIS BOILER MARK 1  ·  14cm DOMED CUBE SS304  ·  COPPER BOWL 10cm  ·  DOME RIVETS  ·  TIG FILLETS",
                FntTitle, lblBrush, x + 10, y + 6);

            using var psiBig = new Font("Courier New", 16f, FontStyle.Bold);
            using var pb = new SolidBrush(bc);
            g.DrawString($"LIVE  {_state.LivePsi:F1} PSI", psiBig, pb, x + 10, y + 24);

            using var detBrush = new SolidBrush(ColBlue);
            g.DrawString($"→  3/4\" STEAM PIPE A     →  3/4\" STEAM PIPE B     →  HALLOWEEN WHISTLE TAP",
                FntValue, detBrush, x + 260, y + 30);

            using var exBrush = new SolidBrush(ColAmber);
            g.DrawString($"EXHAUST A: {_state.ExhaustPsiA:F1} PSI   EXHAUST B: {_state.ExhaustPsiB:F1} PSI   PCW EXHAUST VALVES — PID TIMED",
                FntLabel, exBrush, x + 10, y + 58);

            if (_state.LivePsi >= 110f)
            {
                using var prvBrush = new SolidBrush(ColRed);
                g.DrawString("⚠ APPROACHING PRV 120 PSI", FntLabel, prvBrush, x + w - 220, y + 6);
            }
        }

        // ----------------------------------------------------------------
        //  Thermal panel
        // ----------------------------------------------------------------
        private void DrawThermalPanel(Graphics g, int x, int y, int w)
        {
            int h = 54;
            using var bg = new SolidBrush(Color.FromArgb(20, 14, 8));
            using var border = new Pen(ColBowl, 1.5f);
            g.FillRoundedRect(bg, x, y, w, h, 6);
            g.DrawRoundedRect(border, x, y, w, h, 6);

            using var hdr = new SolidBrush(ColBowl);
            g.DrawString("THERMAL MONITORING — COPPER BOWL · SS304 VESSEL · HEATER ELEMENT 2000–3000W",
                FntLabel, hdr, x + 8, y + 6);

            Color bowlCol = _state.BowlTempC > 400f ? ColRed
                            : _state.BowlTempC > 200f ? ColAmber : ColGreen;
            Color vesselCol = _state.VesselTempC > 200f ? ColRed
                            : _state.VesselTempC > 150f ? ColAmber : ColGreen;
            Color heatCol = _state.HeaterOn ? ColAmber : ColMuted;

            using var tb = new SolidBrush(ColText);
            int col = w / 4;
            DrawDataPair(g, x + 8, y + 22, "BOWL", $"{_state.BowlTempC:F0}°C", bowlCol);
            DrawDataPair(g, x + col, y + 22, "VESSEL", $"{_state.VesselTempC:F0}°C", vesselCol);
            DrawDataPair(g, x + col * 2, y + 22, "HEATER", _state.HeaterOn
                         ? $"{_state.HeaterWatts:F0}W ON" : "OFF", heatCol);
            DrawDataPair(g, x + col * 3, y + 22, "FLASH POINT",
                         _state.BowlTempC > 160f ? "ABOVE 160°C ✓" : $"NEED {160 - _state.BowlTempC:F0}°C MORE",
                         _state.BowlTempC > 160f ? ColGreen : ColMuted);
        }

        // ----------------------------------------------------------------
        //  Flow test panel
        // ----------------------------------------------------------------
        private void DrawFlowTestPanel(Graphics g, int x, int y, int w)
        {
            int h = 58;
            Color borderCol = _state.FlowTestRunning ? ColGreen : ColMuted;
            using var bg = new SolidBrush(Color.FromArgb(14, 22, 14));
            using var border = new Pen(borderCol, 1.5f);
            g.FillRoundedRect(bg, x, y, w, h, 6);
            g.DrawRoundedRect(border, x, y, w, h, 6);

            using var hdr = new SolidBrush(borderCol);
            g.DrawString("EWI WATER FLOW TEST — TANK → PUMP → DUAL FILTER → EWI → CHECK VALVE → NOZZLE → BOILER",
                FntLabel, hdr, x + 8, y + 6);

            int col = w / 5;
            float runTimeMins = _state.FlowTestElapsedS / 60f;

            DrawDataPair(g, x + 8, y + 22, "ELAPSED", $"{runTimeMins:F1} min", ColText);
            DrawDataPair(g, x + col, y + 22, "CONSUMED", $"{_state.TotalWaterUsedLitres:F3} L", ColWater);
            DrawDataPair(g, x + col * 2, y + 22, "RATE", $"{_state.WaterConsumptionRateLph:F2} L/hr", ColCyan);
            DrawDataPair(g, x + col * 3, y + 22, "TANK LEFT", $"{_state.TankLevelLitres:F2} L", ColWater);
            DrawDataPair(g, x + col * 4, y + 22, "EWI PSI", $"{_state.EwiPumpPsi:F0} PSI", ColBlue);

            // Remaining run time estimate
            if (_state.WaterConsumptionRateLph > 0.001f)
            {
                float hoursLeft = _state.TankLevelLitres / _state.WaterConsumptionRateLph;
                float minsLeft = hoursLeft * 60f;
                using var rb = new SolidBrush(ColMuted);
                g.DrawString($"EST. RUN TIME REMAINING AT CURRENT RATE:  {minsLeft:F0} MIN  ({hoursLeft:F2} HR)",
                    FntAlarm, rb, x + 8, y + 42);
            }
            else
            {
                using var rb = new SolidBrush(ColMuted);
                g.DrawString("NO FLOW — EWI NOT RUNNING OR PUMP STOPPED",
                    FntAlarm, rb, x + 8, y + 42);
            }
        }

        // ----------------------------------------------------------------
        //  Control buttons
        // ----------------------------------------------------------------
        private void DrawButtons(Graphics g, int x, int y, int w)
        {
            int bw = 140, bh = 28, gap = 12;

            _btnRefill = new Rectangle(x, y, bw, bh);
            _btnFlowTest = new Rectangle(x + bw + gap, y, bw, bh);
            _btnHeater = new Rectangle(x + (bw + gap) * 2, y, bw, bh);
            _btnEwi = new Rectangle(x + (bw + gap) * 3, y, bw, bh);
            _btnPid = new Rectangle(x + (bw + gap) * 4, y, bw, bh);

            DrawButton(g, _btnRefill, "↺ REFILL TANK",
                       _state.TankLevelPct < 100f ? ColWater : ColMuted,
                       _state.TankLevelPct < 100f);

            DrawButton(g, _btnFlowTest,
                       _state.FlowTestRunning ? "■ STOP FLOW TEST" : "▶ FLOW TEST",
                       _state.FlowTestRunning ? ColRed : ColGreen, true);

            DrawButton(g, _btnHeater,
                       _state.HeaterOn ? "🔥 HEATER OFF" : "🔥 HEATER ON",
                       _state.HeaterOn ? ColAmber : ColMuted, true);

            DrawButton(g, _btnEwi,
                       _state.EwiPumpRunning ? "⚡ EWI STOP" : "⚡ EWI START",
                       _state.EwiPumpRunning ? ColBlue : ColMuted, true);

            DrawButton(g, _btnPid,
                       _state.PidActive ? "PID ACTIVE" : "PID ENABLE",
                       _state.PidActive ? ColGreen : ColMuted, true);
        }

        private void DrawButton(Graphics g, Rectangle r, string label, Color col, bool enabled)
        {
            Color bgCol = enabled
                ? Color.FromArgb(30, col.R, col.G, col.B)
                : Color.FromArgb(18, 24, 30);
            using var bg = new SolidBrush(bgCol);
            using var border = new Pen(enabled ? col : ColBorder, 1.5f);
            g.FillRoundedRect(bg, r.X, r.Y, r.Width, r.Height, 5);
            g.DrawRoundedRect(border, r.X, r.Y, r.Width, r.Height, 5);

            using var tb = new SolidBrush(enabled ? col : ColMuted);
            SizeF sz = g.MeasureString(label, FntBtn);
            g.DrawString(label, FntBtn, tb,
                r.X + r.Width / 2 - sz.Width / 2,
                r.Y + r.Height / 2 - sz.Height / 2);
        }

        // ----------------------------------------------------------------
        //  Water tank visual gauge (vertical column with level)
        // ----------------------------------------------------------------
        private void DrawWaterTankGauge(Graphics g, int x, int y, int size)
        {
            int tankW = size / 2 - 10;
            int tankH = size;
            int tankX = x + size / 2 - tankW / 2;

            // Outer shell
            using var bg = new SolidBrush(Color.FromArgb(8, 18, 28));
            using var border = new Pen(ColWater, 2f);
            g.FillRoundedRect(bg, tankX, y, tankW, tankH, 8);
            g.DrawRoundedRect(border, tankX, y, tankW, tankH, 8);

            // Water fill
            float fillPct = _state.TankLevelPct / 100f;
            int fillH = (int)((tankH - 8) * fillPct);
            int fillY = y + tankH - 4 - fillH;

            Color waterCol = _state.TankLevelCritical ? ColRed
                           : _state.TankLevelLow ? ColAmber : ColWater;

            if (fillH > 2)
            {
                using var wb = new SolidBrush(Color.FromArgb(100, waterCol));
                g.FillRectangle(wb, tankX + 3, fillY, tankW - 6, fillH);
                using var wbSolid = new SolidBrush(Color.FromArgb(180, waterCol));
                g.FillRectangle(wbSolid, tankX + 3, fillY, tankW - 6, 3);
            }

            // Level markers
            for (int pct = 0; pct <= 100; pct += 25)
            {
                int markerY = y + tankH - 4 - (int)((tankH - 8) * pct / 100f);
                using var mp = new Pen(ColMuted, 1f);
                g.DrawLine(mp, tankX - 6, markerY, tankX, markerY);
                using var mb = new SolidBrush(ColMuted);
                g.DrawString($"{pct}%", FntAlarm, mb, tankX - 36, markerY - 5);
            }

            // 15% low water line
            int lowY = y + tankH - 4 - (int)((tankH - 8) * 0.15f);
            using var lowPen = new Pen(ColAmber, 1f) { DashStyle = DashStyle.Dash };
            g.DrawLine(lowPen, tankX, lowY, tankX + tankW, lowY);

            // Labels
            using var titleBrush = new SolidBrush(ColWater);
            SizeF tsz = g.MeasureString("10 GAL", FntLabel);
            g.DrawString("10 GAL", FntLabel, titleBrush, x + size / 2 - tsz.Width / 2, y - 14);

            using var litBrush = new SolidBrush(waterCol);
            SizeF lsz = g.MeasureString($"{_state.TankLevelLitres:F1}L", FntValue);
            g.DrawString($"{_state.TankLevelLitres:F1}L", FntValue, litBrush,
                x + size / 2 - lsz.Width / 2, y + tankH / 2 - 8);

            using var pctBrush = new SolidBrush(waterCol);
            SizeF psz = g.MeasureString($"{_state.TankLevelPct:F0}%", FntLabel);
            g.DrawString($"{_state.TankLevelPct:F0}%", FntLabel, pctBrush,
                x + size / 2 - psz.Width / 2, y + tankH / 2 + 10);
        }

        // ----------------------------------------------------------------
        //  Pressure gauge — radial, 0–500 PSI
        // ----------------------------------------------------------------
        private void DrawPressureGauge(Graphics g, int x, int y, int size)
        {
            float cx = x + size / 2f;
            float cy = y + size / 2f;
            float r = size / 2f - 8f;

            using var bezelBrush = new SolidBrush(Color.FromArgb(16, 22, 30));
            g.FillEllipse(bezelBrush, x, y, size, size);
            using var bezelPen = new Pen(ColBorder, 2f);
            g.DrawEllipse(bezelPen, x, y, size, size);

            DrawGaugeArc(g, cx, cy, r - 10, 145f, 0f, 500f, 0f, 90f, ColGreen, 9f);
            DrawGaugeArc(g, cx, cy, r - 10, 145f, 0f, 500f, 90f, 115f, ColAmber, 9f);
            DrawGaugeArc(g, cx, cy, r - 10, 145f, 0f, 500f, 115f, 120f, ColRed, 9f);
            DrawGaugeArc(g, cx, cy, r - 10, 145f, 0f, 500f, 120f, 500f,
                Color.FromArgb(40, 50, 60), 9f);

            float[] majors = { 0, 90, 120, 150, 200, 250, 300, 350, 400, 450, 500 };
            foreach (float psi in majors)
            {
                float ang = (145f + (psi / 500f) * 290f) * MathF.PI / 180f;
                float cos = MathF.Cos(ang); float sin = MathF.Sin(ang);
                using var tp = new Pen(ColText, 2f);
                g.DrawLine(tp, cx + cos * (r - 4), cy + sin * (r - 4),
                               cx + cos * (r - 16), cy + sin * (r - 16));
                string lbl = psi.ToString("F0");
                SizeF sz = g.MeasureString(lbl, FntAlarm);
                using var tb = new SolidBrush(ColMuted);
                g.DrawString(lbl, FntAlarm, tb,
                    cx + cos * (r - 28) - sz.Width / 2,
                    cy + sin * (r - 28) - sz.Height / 2);
            }

            // Needle
            float live = Math.Clamp(_state.LivePsi, 0f, 500f);
            float nAng = (145f + (live / 500f) * 290f) * MathF.PI / 180f;
            Color nc = live < 90f ? ColGreen : live < 115f ? ColAmber : ColRed;
            using var np = new Pen(nc, 2.5f) { EndCap = LineCap.ArrowAnchor };
            g.DrawLine(np, cx - MathF.Cos(nAng) * 12, cy - MathF.Sin(nAng) * 12,
                           cx + MathF.Cos(nAng) * (r - 20), cy + MathF.Sin(nAng) * (r - 20));
            using var hub = new SolidBrush(nc);
            g.FillEllipse(hub, cx - 6, cy - 6, 12, 12);

            // Digital readout
            string disp = $"{_state.LivePsi:F0}";
            SizeF dsz = g.MeasureString(disp, FntBig);
            using var db = new SolidBrush(nc);
            g.DrawString(disp, FntBig, db, cx - dsz.Width / 2, cy + 24);
            using var ub = new SolidBrush(ColMuted);
            SizeF usz = g.MeasureString("PSI", FntSub);
            g.DrawString("PSI", FntSub, ub, cx - usz.Width / 2, cy + 50);

            // EWI PSI secondary
            using var epb = new SolidBrush(ColBlue);
            string ewiStr = $"EWI {_state.EwiPumpPsi:F0} PSI";
            SizeF esz = g.MeasureString(ewiStr, FntAlarm);
            g.DrawString(ewiStr, FntAlarm, epb, cx - esz.Width / 2, cy + 62);

            using var gtb = new SolidBrush(ColMuted);
            SizeF gtsz = g.MeasureString("BOILER PRESSURE", FntAlarm);
            g.DrawString("BOILER PRESSURE", FntAlarm, gtb, cx - gtsz.Width / 2, y + 6);
        }

        private static void DrawGaugeArc(Graphics g, float cx, float cy, float r,
            float startDeg, float psiMin, float psiMax,
            float segStart, float segEnd, Color col, float width)
        {
            float sweep = ((segEnd - segStart) / (psiMax - psiMin)) * 290f;
            float start = startDeg + ((segStart - psiMin) / (psiMax - psiMin)) * 290f;
            using var p = new Pen(col, width) { LineJoin = LineJoin.Round };
            g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, start, sweep);
        }

        // ----------------------------------------------------------------
        //  Throttle lever
        // ----------------------------------------------------------------
        private void DrawThrottle(Graphics g, int x, int y, int w, int h)
        {
            _throttleTrackRect = new Rectangle(x + w / 2 - 6, y, 12, h);

            using var tb = new SolidBrush(Color.FromArgb(22, 30, 38));
            using var tbp = new Pen(ColBorder, 1.5f);
            g.FillRoundedRect(tb, _throttleTrackRect.X, _throttleTrackRect.Y,
                _throttleTrackRect.Width, _throttleTrackRect.Height, 6);
            g.DrawRoundedRect(tbp, _throttleTrackRect.X, _throttleTrackRect.Y,
                _throttleTrackRect.Width, _throttleTrackRect.Height, 6);

            // Zone fills
            float[] zones = { 0f, 0.18f, 0.55f, 1f };
            Color[] zc = { ColRed, ColAmber, ColGreen };
            for (int i = 0; i < zc.Length; i++)
            {
                int zy = y + (int)(zones[i] * h);
                int zh = (int)((zones[i + 1] - zones[i]) * h);
                using var zb = new SolidBrush(Color.FromArgb(55, zc[i]));
                g.FillRectangle(zb, _throttleTrackRect.X + 1, zy,
                    _throttleTrackRect.Width - 2, zh);
            }

            // Detent labels
            var detents = new (float n, string lbl)[]
            {
                (0f,    "500 PSI"), (0.38f, "250 PSI"), (1f, " 90 PSI")
            };
            foreach (var (n, lbl) in detents)
            {
                int dy = y + (int)(n * h);
                using var lp = new Pen(ColBorder, 1f);
                g.DrawLine(lp, x, dy, x + w, dy);
                using var lb = new SolidBrush(ColMuted);
                g.DrawString(lbl, FntAlarm, lb, x - 50, dy - 6);
            }

            // Handle
            float ni = 1f - _throttleNorm;
            int hY = y + (int)(ni * (h - 28));
            int hX = x + w / 2 - 18;
            Color hc = _throttleNorm < 0.18f ? ColGreen
                      : _throttleNorm < 0.55f ? ColAmber : ColRed;

            using var hbg = new SolidBrush(Color.FromArgb(35, 45, 55));
            using var hbp = new Pen(hc, 2.5f);
            g.FillRoundedRect(hbg, hX, hY, 36, 28, 5);
            g.DrawRoundedRect(hbp, hX, hY, 36, 28, 5);

            for (int gl = 0; gl < 3; gl++)
            {
                using var gp = new Pen(Color.FromArgb(80, hc), 1f);
                g.DrawLine(gp, hX + 5, hY + 7 + gl * 6, hX + 31, hY + 7 + gl * 6);
            }

            string psiTxt = $"{_state.TargetPsi:F0}";
            SizeF psz = g.MeasureString(psiTxt, FntAlarm);
            using var pb = new SolidBrush(hc);
            g.DrawString(psiTxt, FntAlarm, pb,
                hX + 18 - psz.Width / 2, hY + 14 - psz.Height / 2);

            using var ttb = new SolidBrush(ColMuted);
            g.DrawString("THROTTLE", FntAlarm, ttb, x - 4, y - 18);
            g.DrawString("TARGET", FntAlarm, ttb, x + 2, y - 8);
        }

        // ----------------------------------------------------------------
        //  Alarm panel
        // ----------------------------------------------------------------
        private void DrawAlarmPanel(Graphics g, int x, int y, int w)
        {
            int h = 48;
            Color bc = _state.ActiveAlarms.Count > 0 ? ColRed : ColBorder;
            using var bg = new SolidBrush(Color.FromArgb(28, 8, 8));
            using var bp = new Pen(bc, 1.5f);
            g.FillRoundedRect(bg, x, y, w, h, 5);
            g.DrawRoundedRect(bp, x, y, w, h, 5);

            using var hdr = new SolidBrush(ColRed);
            g.DrawString("⚠ ALARMS", FntLabel, hdr, x + 8, y + 5);

            if (_state.ActiveAlarms.Count == 0)
            {
                using var ok = new SolidBrush(ColGreen);
                g.DrawString("ALL SYSTEMS NOMINAL", FntLabel, ok, x + 90, y + 5);
            }
            else
            {
                int ax = x + 90;
                foreach (var alarm in _state.ActiveAlarms)
                {
                    using var ab = new SolidBrush(ColRed);
                    g.DrawString($"■ {alarm}", FntLabel, ab, ax, y + 5);
                    ax += 200;
                    if (ax > x + w - 200) { ax = x + 90; }
                }
            }

            // Second row — sensor readings
            using var sr = new SolidBrush(ColMuted);
            g.DrawString(
                $"FILTER ΔP: {_state.FilterDeltaP:F1} PSI  |  " +
                $"BOWL: {_state.BowlTempC:F0}°C  |  " +
                $"VESSEL: {_state.VesselTempC:F0}°C  |  " +
                $"ACCUM: {_state.EwiAccumulatorPsi:F0} PSI  |  " +
                $"PULSE: {_state.InjectionPulseMs:F1}ms  |  " +
                $"FLOW: {_state.EwiFlowRateMlpm:F0} ml/min  |  " +
                $"CONSUMED TOTAL: {_state.TotalWaterUsedLitres:F3} L",
                FntAlarm, sr, x + 8, y + 28);
        }

        // ----------------------------------------------------------------
        //  Status bar
        // ----------------------------------------------------------------
        private void DrawStatusBar(Graphics g, int x, int y, int w)
        {
            using var lp = new Pen(ColBorder, 1f);
            g.DrawLine(lp, x, y, x + w, y);

            string s = _state.PidCutoff
                ? "⚠  PID CUTOFF ACTIVE  —  EWI INJECTION HALTED  —  BOILER > 115 PSI  —  THROTTLE BACK TO RESUME"
                : $"●  SYS: {(_state.FlowTestRunning ? "FLOW TEST" : "STANDBY")}  " +
                  $"|  BOILER {_state.LivePsi:F1} PSI  TARGET {_state.TargetPsi:F0} PSI  " +
                  $"|  TANK {_state.TankLevelLitres:F1}L ({_state.TankLevelPct:F0}%)  " +
                  $"|  PUMP {_state.PumpFlowLpm:F1} L/min  " +
                  $"|  EWI {_state.EwiPumpPsi:F0} PSI  " +
                  $"|  STM32 ● MOCK";

            Color sc = _state.PidCutoff ? ColRed : ColGreen;
            using var sb = new SolidBrush(sc);
            g.DrawString(s, FntAlarm, sb, x, y + 5);
        }

        // ----------------------------------------------------------------
        //  Utility — data pair label/value
        // ----------------------------------------------------------------
        private void DrawDataPair(Graphics g, int x, int y, string label, string val, Color valCol)
        {
            using var lb = new SolidBrush(ColMuted);
            using var vb = new SolidBrush(valCol);
            g.DrawString(label, FntAlarm, lb, x, y);
            g.DrawString(val, FntValue, vb, x, y + 12);
        }

        // ================================================================
        //  Mouse — throttle drag + button clicks
        // ================================================================
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            // Buttons
            if (_btnRefill.Contains(e.Location))
            {
                _state.TankLevelLitres = _state.TankCapacityLitres;
                _state.TotalWaterUsedLitres = 0f;
                return;
            }
            if (_btnFlowTest.Contains(e.Location))
            {
                _state.FlowTestRunning = !_state.FlowTestRunning;
                if (_state.FlowTestRunning)
                {
                    _state.FlowTestElapsedS = 0f;
                    _state.TotalWaterUsedLitres = 0f;
                    _state.EwiPumpRunning = true;
                    _state.PumpRunning = true;
                    _state.HeaterOn = true;
                    _state.HeaterWatts = 2500f;
                    _state.PidActive = true;
                }
                else
                {
                    _state.EwiPumpRunning = false;
                    _state.HeaterOn = false;
                    _state.PidActive = false;
                }
                return;
            }
            if (_btnHeater.Contains(e.Location))
            {
                _state.HeaterOn = !_state.HeaterOn;
                _state.HeaterWatts = _state.HeaterOn ? 2500f : 0f;
                return;
            }
            if (_btnEwi.Contains(e.Location))
            {
                _state.EwiPumpRunning = !_state.EwiPumpRunning;
                return;
            }
            if (_btnPid.Contains(e.Location))
            {
                _state.PidActive = !_state.PidActive;
                return;
            }

            // Throttle drag
            if (_throttleTrackRect.Contains(e.Location))
            {
                _dragging = true;
                UpdateThrottle(e.Y);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging) UpdateThrottle(e.Y);
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }

        private void UpdateThrottle(int mouseY)
        {
            float n = (mouseY - _throttleTrackRect.Top) / (float)_throttleTrackRect.Height;
            _throttleNorm = 1f - Math.Clamp(n, 0f, 1f);
            _state.TargetPsi = 90f + _throttleNorm * 410f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _simTimer?.Dispose();
            base.Dispose(disposing);
        }
    }

    // ================================================================
    //  Graphics extension helpers
    // ================================================================
    internal static class GfxExt
    {
        public static void FillRoundedRect(this Graphics g, Brush b, int x, int y, int w, int h, int r)
        {
            using var path = RoundedPath(x, y, w, h, r);
            g.FillPath(b, path);
        }
        public static void DrawRoundedRect(this Graphics g, Pen p, int x, int y, int w, int h, int r)
        {
            using var path = RoundedPath(x, y, w, h, r);
            g.DrawPath(p, path);
        }
        private static GraphicsPath RoundedPath(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}