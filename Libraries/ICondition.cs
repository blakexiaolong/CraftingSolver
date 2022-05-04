namespace CraftingSolver
{
    public enum ConditionQuality { Poor, Normal, Good, Excellent }
    public interface ICondition
    {
        bool IgnoreConditions { get; set; }
        double PPGood { get; set; }
        double PPExcellent { get; set; }
        ConditionQuality ConditionQuality { get; set; }

        bool CheckGoodOrExcellent();
        double PGoodOrExcellent();
    }

    public class SimulatorCondition : ICondition
    {
        public bool IgnoreConditions { get; set; }
        public double PPGood { get; set; }
        public double PPExcellent { get; set; }
        public ConditionQuality ConditionQuality { get; set; }

        public bool CheckGoodOrExcellent() => IgnoreConditions || ConditionQuality == ConditionQuality.Good || ConditionQuality == ConditionQuality.Excellent;
        public double PGoodOrExcellent() => IgnoreConditions ? 1 : PPGood + PPExcellent;
    }

}
