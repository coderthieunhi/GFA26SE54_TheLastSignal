namespace TheLastSignal.Core
{
    /// <summary>
    /// The three Survivor role kits (slots 0-2). Slot 3 is always the Shade (see
    /// PlayerController.SlotIndex / GameCharacterRole.forSlot on the backend) and never gets one of
    /// these — role assignment only ever covers the three Survivor slots.
    /// </summary>
    public enum SurvivorRole
    {
        Engineer,
        Mechanic,
        Logistics
    }
}
