# WindowKeeper
A utility that saves window positions before screen resolution/monitor count changes, and restores them when those settings are restored

Find the latest release here: https://github.com/Ricimon/WindowKeeper/releases

---

A big problem with DisplayPort is that it disconnects itself when the monitor it's attached to is turned off whether manually or automatically by screen shutoff (standard or lock-screen idling). This means that Windows will stop detecting the display, and move any windows that were originally on the display to other available displays (or size them super small if the only monitor connected is DisplayPort). This utility saves window positions (through Windows event hooks, so no polling) and detects screen resolution/monitor count changes so that window positions are restored when a DisplayPort monitor reconnects.

Much of the shell code surrounding the main processor was adapted from http://www.ninjacrab.com/persistent-windows/ since this is my first WPF project.

I also made this because http://www.ninjacrab.com/persistent-windows/ and https://github.com/hunkydoryrepair/MonitorKeeper didn't work for me.

---

If you'd like to mess around with the source code, here's a couple things to get you started:

* The active project should be `Ricimon.WindowKeeper.SystrayShell`, as this initializes both the core program and the WPF diagnostics window.
* Core program logic can be found in `Ricimon.WindowKeeper.Common.WindowKeeperProcessor`.
* App initialization happens in `Ricimon.WindowKeeper.SystrayShell.Program`.
* The diagnostics window will auto-open in Debug mode. This is controlled in `Ricimon.WindowKeeper.SystrayShell.SystrayForm` (code-behind).
