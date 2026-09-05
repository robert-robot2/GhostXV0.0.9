namespace RoboutesTentacleBurgers.Pages
{
    public partial class SteamElectrical
    {


        // ============================================================
        // PHASE REGISTRY — add one entry per new phase
        // ============================================================
        private record PhaseEntry(string Id, string Label, string Name);

        private readonly List<PhaseEntry> _phases = new()
    {
        new("phase1", "Phase 1", "Steam-Electric Concept"),
        new("phase2", "Phase 2", "Miasma Electrical Engine"),

        // ── Uncomment and add as you build each phase ──
         new("phase3", "Phase 3", "Rail-Linear Piston"),
         new("phase4", "Phase 4", "Rail-Driven Transmission"),
        new("phase5", "Phase 5", "Dual-Rail Rotary Converter"),
         new("phase6", "Phase 6", "Safety & Cost Engineering"),
         new("phase7", "Phase 7", "Dual-Rail Regenerative Loop"),
         new("phase8", "Phase 8", "Grid-Solar Hybrid Steam Electric"),
       new("phase9", "Phase 9", "Self-Sustaining Steam Dynamo"),
              new("phase10", "Phase 10", "Self-Sustaining Steam Dynamo"),
            new("phase11", "Phase 11", "Piston-Less Static Mechanical Engine — Corrected Architecture"),
               new("phase12", "Phase 12", "Piston-Less Static Mechanical Engine — Corrected Architecture"),
                      new("phase13", "Phase 13", "Rhombic"),
                            new("phase14", "Phase 14", "Casualty"),
                                new("phase15", "Phase 15", "World Domination"),
            new("phase16", "Phase 16", "Chevy Steamer"),
                new("phase17", "Phase 17", "Chevy Miasma"),
                  new("phase18", "Phase 18", "Chevy MiasmaV2"),
                     new("phase19", "Phase 19", "Chevy MiasmaV3"),
                       new("phase20", "Phase 20", "Chevy Miasma V4"),
                        new("phase21", "Phase 21", "Chevy Miasma V5"),
                           new("phase22", "Phase 22", "Chevy Miasma V6"),
                             new("phase23", "Phase 23", "Chevy Miasma V7"),
                             new ("phase24", "Phase 24", "Chevy Miasma V8"),
    }; 

        private string _activeId = "phase1";

        private void SetPhase(string id) => _activeId = id;

        private void ScrollTabsLeft() => JS.InvokeVoidAsync("seScrollTabs", -1);
        private void ScrollTabsRight() => JS.InvokeVoidAsync("seScrollTabs", 1);

        [Inject] private IJSRuntime JS { get; set; } = default!;

        public void Dispose() { }

    }










}
