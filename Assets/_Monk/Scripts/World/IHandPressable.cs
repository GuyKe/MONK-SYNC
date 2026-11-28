using Monk.Locomotion;

namespace Monk.World
{
    /// <summary>Implemented by buttons and levers. Called when a hand makes contact with their collider.</summary>
    public interface IHandPressable
    {
        void Press(HandContactEvent contact);
    }
}
