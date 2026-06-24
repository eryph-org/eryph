// The generic change-tracking core (interceptor base, queue, handler interface, background service)
// was promoted to Eryph.ModuleCore so it can be shared with the identity module. Expose it project-wide
// so the existing change-tracking files resolve it without a per-file using.

global using Eryph.ModuleCore.ChangeTracking;
