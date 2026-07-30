namespace LibreLms.Modules.Scorm;

/// <summary>
/// No behavior — exists so tests and DI wiring have a stable type to anchor on
/// (<c>typeof(ModuleMarker).Assembly</c>) before this module has any real types yet.
/// Delete once a real type in this assembly can serve the same purpose.
/// </summary>
public sealed class ModuleMarker;
