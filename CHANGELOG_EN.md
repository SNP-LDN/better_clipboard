# Better Clipboard Changelog

**Language:** [中文](CHANGELOG_CN.md) | English

This file documents notable feature and interface changes to Better Clipboard.

## 1.0.8 - 2026-07-28

### Improved

- Update packages now use four parallel download segments when supported, with automatic fallback to a resumable single-stream download.
- Users can choose the update package download directory after confirming an update, and the downloaded package remains in that location.
- The update window now shows downloaded size, total size, and live download speed.
- The connection timeout is now 15 seconds so a failed connection no longer remains at zero bytes for an extended period.

### Fixed

- Better Clipboard now allows only one background instance, preventing competing update checks, locked update state, and duplicate downloads.
- A downloaded package is now checked against the expected pending version before installation and restart.
- Duplicate background processes left by older versions are stopped before applying an update so they cannot keep the old application files locked.

## 1.0.7 - 2026-07-28

### Added

- Added custom favorite folders with create, filter, and delete actions. New favorites are placed in the Base Favorites folder by default.
- Deleting a custom folder now moves its contents to Base Favorites without losing their favorite status.
- Added checkbox-based batch actions for adding or moving items to a folder, with selections cleared automatically after completion.
- Added a batch Remove from Favorites action. Removed items remain available in regular clipboard history.
- Added a title-bar pin toggle. A pinned window stays on top and remains open when it loses focus, pastes an item, or opens an image preview.

### Improved

- The most recently copied item now always appears first, including existing items copied again.
- Favorite items now use a yellow filled star and a complete yellow card border for clearer recognition.
- Moving items to a favorite folder now requires pressing Confirm after choosing the destination, reducing accidental moves.
- Remove from Favorites now appears as a separate management action instead of sharing the add-or-move row.
- Folder deletion, selected-item deletion, default restoration, and settings validation prompts now use an in-app confirmation layer instead of separate system windows.

### Fixed

- Fixed folder selectors displaying internal object IDs and type names instead of folder names.
- Fixed the main popup closing from focus loss before the Delete Folder confirmation could appear.
- Fixed the yellow bottom border of favorite cards being hidden by adjacent list items.

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
