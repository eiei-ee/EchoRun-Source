// Compatibility entry for earlier review scripts. The active palette has one
// authoring owner; this does not restore the superseded Night Grape colors.
public static class NightGrapeWorldInstaller
{
    public static void Install() => LayeredMemoryPalette.Install();
}
