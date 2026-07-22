# Better Clipboard Changelog

**Language:** [中文](CHANGELOG_CN.md) | English

This file documents notable feature and interface changes to Better Clipboard.

## 1.0.4 - 2026-07-22

### Fixed

- Fixed a duplicate close request when the history window lost focus that could silently terminate the background application.
- Fixed rows of empty icon boxes appearing on the left side of the notification-area context menu.
- Unhandled UI exceptions now show an error report message and save the complete exception and call site to the diagnostic log.
- The diagnostic log now records normal application exits and exit codes, making expected exits distinguishable from crashes.

## 1.0.3 - 2026-07-22

### Added

- Added a clear install or update completion message after the first successful launch of each installed version, including background status, startup status, and the installation path.

### Fixed

- Fixed an incorrect native notification-area API entry point that prevented the application from starting after installation.
- A notification-area initialization failure no longer terminates the whole app; clipboard monitoring and the global shortcut remain available.

## 1.0.2 - 2026-07-22

Last updated: 2026-07-22

### Added

- Added a live private-memory indicator to the history window footer. It refreshes every two seconds and releases its monitoring resources when the window closes.
- Integrated Velopack installation and online updates through GitHub Releases.
- Added the current version and a manual Check for Updates action to the settings window.
- The installer now creates Desktop, Start Menu, and startup shortcuts by default.
- Added light, dark, and Windows system theme modes.
- Added standard and soft-glass visual presets.
- Glass opacity can now be adjusted from 55% to 95% with a live preview when the glass preset is selected.
- Added checkboxes for selecting multiple history items and deleting them together with the Delete Selected action.
- The batch-delete button shows the number of selected items and asks for confirmation before deletion.
- Added the Better Clipboard brand logo across windows, the title bar, the system tray, and the executable icon.
- Gave the clipboard surface in the small app icon a white fill while keeping the exterior transparent for better visibility on dark backgrounds.

### Improved

- Replaced the WinForms tray component with the native Windows notification-area API to reduce idle memory and loaded assemblies.
- Closing the history window now releases its control tree, collections, timer handlers, and image thumbnails instead of keeping a hidden window alive.
- Image thumbnails are now loaded on demand and retained in a bounded LRU cache to prevent memory growth with large histories.
- The clipboard history window can now be moved by dragging its title bar.
- Reduced the brightness of selected items, tabs, and drop-down options for calmer feedback in dark mode.
- Widened the item action area and stabilized button dimensions so Favorite and Delete controls are no longer compressed.
- Refined the Favorite and Delete icons, spacing, and tooltips for clearer and more accurate interaction.
- Improved drop-downs, tabs, and hover, selected, and disabled states across themes.
- Theme and glass opacity controls are now available consistently in both the history popup and the standalone settings window.

### Fixed

- Fixed Favorite and Delete buttons being squeezed or becoming difficult to read in narrower windows.
- Fixed clicks on checkboxes and action buttons potentially opening or pasting a clipboard item.
- Fixed the current item selection being lost when the list refreshes.
- Fixed the app using a default or low-contrast icon in dark title bars and the Windows system tray.
