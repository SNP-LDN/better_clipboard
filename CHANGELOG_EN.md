# Better Clipboard Changelog

**Language:** [中文](CHANGELOG_CN.md) | English

This file documents notable feature and interface changes to Better Clipboard.

## Unreleased

Last updated: 2026-07-20

### Added

- Added light, dark, and Windows system theme modes.
- Added standard and soft-glass visual presets.
- Glass opacity can now be adjusted from 55% to 95% with a live preview when the glass preset is selected.
- Added checkboxes for selecting multiple history items and deleting them together with the Delete Selected action.
- The batch-delete button shows the number of selected items and asks for confirmation before deletion.
- Added the Better Clipboard brand logo across windows, the title bar, the system tray, and the executable icon.
- Gave the clipboard surface in the small app icon a white fill while keeping the exterior transparent for better visibility on dark backgrounds.

### Improved

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
