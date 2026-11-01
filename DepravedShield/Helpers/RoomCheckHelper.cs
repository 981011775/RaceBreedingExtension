using MBMScripts;

namespace DepravedShield.Helpers
{
    public static class RoomCheckHelper
    {
        public static bool IsInKokiRoom(Character character)
        {
            if (character == null) return false;
            return character.Room is KokiRoom;
        }
    }
}