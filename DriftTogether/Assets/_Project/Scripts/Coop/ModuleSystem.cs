namespace DriftTogether.Coop
{
    public enum RaftSlot
    {
        Bow = 0,     // парус
        Midship = 1, // тент
        Stern = 2    // верстак
    }

    /// <summary>
    /// UC-13: raft modules. Each slot hosts one fixed module built from
    /// timber; every module shifts the trim (центровка) making the raft
    /// easier to heel. Choice is irreversible until a rebuild — по доке.
    /// Pure logic.
    /// </summary>
    public sealed class ModuleSystem
    {
        public const int SailCost = 4;
        public const int CanopyCost = 3;
        public const int WorkbenchCost = 3;

        public const float SailSpeedBonus = 1.3f;
        public const float SailTurnPenalty = 0.8f;
        public const float WorkbenchRepairFactor = 2f;
        public const float TiltFactorPerModule = 0.08f;

        int _mask;

        public int Mask => _mask;

        public void LoadMask(int mask) => _mask = mask & 0b111;

        public bool Has(RaftSlot slot) => (_mask & (1 << (int)slot)) != 0;

        public static int CostOf(RaftSlot slot)
        {
            switch (slot)
            {
                case RaftSlot.Bow: return SailCost;
                case RaftSlot.Midship: return CanopyCost;
                default: return WorkbenchCost;
            }
        }

        public static string NameOf(RaftSlot slot)
        {
            switch (slot)
            {
                case RaftSlot.Bow: return "Парус";
                case RaftSlot.Midship: return "Тент";
                default: return "Верстак";
            }
        }

        /// <summary>Returns the timber cost if installed, or -1 when impossible.</summary>
        public int TryInstall(RaftSlot slot, int logsAvailable)
        {
            if (Has(slot))
                return -1;
            int cost = CostOf(slot);
            if (logsAvailable < cost)
                return -1;
            _mask |= 1 << (int)slot;
            return cost;
        }

        public int InstalledCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 3; i++)
                    if ((_mask & (1 << i)) != 0)
                        count++;
                return count;
            }
        }

        // ---------- Effects ----------

        public float SpeedMultiplier => Has(RaftSlot.Bow) ? SailSpeedBonus : 1f;
        public float TurnMultiplier => Has(RaftSlot.Bow) ? SailTurnPenalty : 1f;
        public bool DryingEverywhere => Has(RaftSlot.Midship);
        public float RepairSpeedMultiplier => Has(RaftSlot.Stern) ? WorkbenchRepairFactor : 1f;

        /// <summary>Модули поднимают центр тяжести: крен растёт быстрее.</summary>
        public float TiltFactor => 1f + InstalledCount * TiltFactorPerModule;
    }
}
