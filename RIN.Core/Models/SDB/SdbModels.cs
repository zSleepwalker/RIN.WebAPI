using System;
using System.Collections.Generic;

namespace RIN.Core.Models.SDB
{
    public class SdbItem
    {
        public int SdbId { get; set; }
        public uint NameId { get; set; }
        public uint DescriptionId { get; set; }
        public int Quality { get; set; }
        public int TierId { get; set; }
        
        // Links to abilities
        public List<int> AbilityIds { get; set; } = new List<int>();
    }

    public class SdbAbility
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<SdbPhase> Phases { get; set; } = new List<SdbPhase>();
    }

    public class SdbPhase
    {
        public int Id { get; set; }
        public int Ordinal { get; set; }
        public List<SdbCommand> Commands { get; set; } = new List<SdbCommand>();
    }

    public class SdbCommand
    {
        public int Id { get; set; }
        public string CommandType { get; set; } = string.Empty; // e.g. "ApplyStatusEffect", "StatModifier"
        public Dictionary<string, object> Arguments { get; set; } = new Dictionary<string, object>();
    }

    public class SdbStatusEffect
    {
        public int Id { get; set; }
        public float Duration { get; set; }
        public List<SdbModifier> Modifiers { get; set; } = new List<SdbModifier>();
    }

    public class SdbModifier
    {
        public string Attribute { get; set; } = string.Empty;
        public float Value { get; set; }
        public string Operation { get; set; } = string.Empty; // e.g. "Add", "Multiply"
    }

    public class SdbBlueprint
    {
        public int Id { get; set; }
        public int MainOutputItemId { get; set; }
        public int BuildTimeSecs { get; set; }
        public List<SdbBlueprintItem> Items { get; set; } = new List<SdbBlueprintItem>();
    }

    public class SdbBlueprintItem
    {
        public int ItemSdbId { get; set; }
        public int Quantity { get; set; }
        public bool IsOutput { get; set; }
    }

    public class SdbStarterLoadoutSlot
    {
        public int SlotType { get; set; }
        public int DefaultPveModule { get; set; }
        public int DefaultPvpModule { get; set; }
    }
}
