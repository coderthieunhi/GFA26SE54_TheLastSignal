using System;
using UnityEngine;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities.Interaction
{
    /// <summary>
    /// Legacy duplicate interface. Inherits from <see cref="TheLastSignal.Core.Interaction.IInteractable"/>
    /// for unified compatibility. Please migrate usages to <see cref="TheLastSignal.Core.Interaction.IInteractable"/>.
    /// </summary>
    [Obsolete("Use TheLastSignal.Core.Interaction.IInteractable instead.")]
    public interface IInteractable : TheLastSignal.Core.Interaction.IInteractable
    {
        void Interact(GameObject player);
    }
}
