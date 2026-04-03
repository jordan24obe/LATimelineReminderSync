namespace LATimelineReminderSync.Models;

public class EncounterManifest
{
    public List<EncounterEntry> Encounters { get; set; } = new();
}

public class EncounterEntry
{
    public int EncounterId { get; set; }
    public string EncounterName { get; set; } = "";
    public int DifficultyIndex { get; set; }  // 1 = Normal, 2 = Heroic/Mythic
    public string FileName { get; set; } = "";  // relative path, e.g., "The-Voidspire/mythic/Imperator-Averzian.lua"
}
