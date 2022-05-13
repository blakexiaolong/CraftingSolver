namespace Libraries
{
    public class StateViolations
    {
        public bool ProgressOk { get; init; }
        public bool CpOk { get; init; }
        public bool DurabilityOk { get; init; }
        public bool TrickOk { get; init; }
        public bool ReliabilityOk { get; init; }

        public bool AnyIssues() => !ProgressOk || !CpOk || !DurabilityOk || !TrickOk || !ReliabilityOk;
        public bool FailedCraft() => (!CpOk || !DurabilityOk || !TrickOk || !ReliabilityOk) && !ProgressOk;
    }
}
