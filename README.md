# WindowOperatorUI - Updated Features

## Recent Changes

### 1. Flexible Window Sizing
- Windows can now retain their original size when launched
- Width and Height fields are now optional in the UI. Leaving them empty will not modify window sizes
- WindowSelector includes a checkbox to preserve original window size when selecting a window

### 2. Executable Path Management
- Path editing feature added to the UI
- Added a dedicated path edit button with browse dialog
- WindowSelector now allows custom path input regardless of the selected window

### 3. Improved UI Notifications
- Replaced blocking message boxes with toast notifications
- Added notification service for consistent UI messaging
- Notifications appear as non-modal toasts that auto-close after a few seconds

## Using the Application

### Window Configuration
- To create a new window configuration, click the "Add New" button
- To select an existing window, click the "Select Window" button (crosshair icon)
- Check "Keep original size" in the selector to preserve the window's dimensions

### Path Management
- Click the pencil (✏) icon to edit the executable path
- Use the "Browse..." button to select a path from disk
- Custom paths can be entered directly in the text field

### Running Configurations
- Click the play (▶) button to run a single configuration
- Use checkboxes to select multiple configurations
- Click "Run Selected" to launch all selected configurations

## Notes
- Empty Width/Height values will be treated as null and won't resize windows
- Notifications are automatically displayed and dismissed
- Window positions (X, Y coordinates) are always applied 