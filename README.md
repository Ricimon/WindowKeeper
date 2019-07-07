# WindowKeeper
A utility that saves window positions before screen resolution/monitor count changes, and restores them when those settings are restored

### This is a work-in-progress

A big problem with DisplayPort is that it disconnects itself when the monitor it's attached to is turned off whether manually or automatically by screen shutoff. This means that Windows will stop detecting the display, and move any windows that were originally on the display to other available displays (or size them super small if the only monitor connected is DisplayPort). This utility saves window positions (through Windows event hooks, so no polling) and detects screen resolution/monitor count changes so that window positions are restored when a DisplayPort monitor reconnects.

Much of the shell code surrounding the main processor was adapted from http://www.ninjacrab.com/persistent-windows/ since this is my first WPF project.

I'm also working on this because http://www.ninjacrab.com/persistent-windows/ and https://github.com/hunkydoryrepair/MonitorKeeper didn't work for me.
