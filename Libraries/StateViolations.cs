namespace CraftingSolver
{
    public class StateViolations
    {
        public bool ProgressOk { get; set; }
        public bool CpOk { get; set; }
        public bool DurabilityOk { get; set; }
        public bool TrickOk { get; set; }
        public bool ReliabilityOk { get; set; }

        public bool AnyIssues() => !ProgressOk || !CpOk || !DurabilityOk || !TrickOk || !ReliabilityOk;
        public bool FailedCraft() => (!CpOk || !DurabilityOk || !TrickOk || !ReliabilityOk) && !ProgressOk;
    }
}
