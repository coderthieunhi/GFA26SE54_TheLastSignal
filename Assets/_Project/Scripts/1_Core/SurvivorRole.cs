namespace TheLastSignal.Core
{
    /// <summary>
    /// The three Survivor role kits (slots 0-2). Slot 3 is always the Shade (see
    /// PlayerController.SlotIndex / GameCharacterRole.forSlot on the backend) and never gets one of
    /// these — role assignment only ever covers the three Survivor slots.
    /// F-07 & GDD 7.1: Drone Operator, Engineer, Logistics.
    /// </summary>
    public enum SurvivorRole
    {
        Engineer = 0,
        DroneOperator = 1,
        Mechanic = 1, // Backwards-compatible alias for prototype codebase
        Logistics = 2
    }
}
