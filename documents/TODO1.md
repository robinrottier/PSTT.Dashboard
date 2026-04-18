# TO DO


## App
- add common data directory for file saving and loading, between WebAppServer and WebAppWasm projects
- that data directory should come from config file and environment variable
- default file is diagram.json but other files will be available

## Layout
- add a conventional looking top-level menu for most new functionality
- button in top right using 3 bars icon
- structured as follows:
...
	File
		New
		Open
		Save
		Save As
		Reload
	Edit
		Add Node
		Delete Node
		seperator
		Cut selected nodes
		Copy selected nodes
		Paste nodes
		Seperator
		Add port
			Top
			Left
			Bottom
			Right
		Delete port
			Top
			Left
			Bottom
			Right
		Properties
	Options
		Theme
			Light
			Dark
			Auto
		Show
			Diagram name in title bar
		Grid (applies when in edit only)
			None
			Small (5px)
			Medium (10px)
			Large (20px)
	Data			
	About
...			
- top panel in current layout should be:
	Icon (perhaps something looking lke a flow chart or diagram)
	Title as {AppName} and optional {Diagram name) (optioanl from a 
	-space to justify to right-
	Edit mode switch (to go into edit mode or back to display mode)
	Menu button
- remove the current side panel and toolbar in editor mode


## Nodes
- should have optional 2nd data element
- should have format options for each item of data e.g. number dec places, units string appeanded
- format should be some sort of string interpolation with c# string format notation
- font size property
- optional value coloring (e.g. <0 then red)

## Edit page
- double click on a node and it should go to edit properties


---

# Completed

## App Layout
- overall title reads app name from assembly (MqttDashboard.csproj → `GetType().Assembly.GetName().Name`)
- removed the "..." (MoreVert) button from top right corner
- removed Weather page from project and menus
- menu now shows: Home | Diagram Edit | Data | About
- current Diagram page is the editor with existing toolbar, grid etc (renamed label to "Diagram Edit")
- added new readonly `DiagramDisplay.razor` page at route "/"
	- readonly — all nodes locked, no toolbar, no editing or saving
	- implemented as a separate page that shares the Blazor Diagrams canvas component
	- display-only page is default startup (route "/")
	- first in menu, called "Home"

## About page (previously "Home")
- renamed to "About", moved to last in the menu, route "/about"
- shows info in a table layout:
	- project name and version number from assembly
	- dependency versions: Blazor Diagrams, MudBlazor
	- .NET version (RuntimeInformation.FrameworkDescription)
	- server connection status (previously on MQTT/Data page)
	- debug-only section (#if DEBUG): render mode (WASM/Server), server host, current URL
	- uses extensible `List<InfoItem(Label, Value)>` pattern for easy additions

## Data page
- page title changed to "Data"
- layout: explorer tree on left (md=8), subscriptions + log stacked on right (md=4)
- fluid/responsive: vertically stacked on narrow views with explorer first

### Subscriptions panel
- card header renamed to "Subscriptions"
- compact list with minimal padding (Dense=true, py-0 classes)
- most recently subscribed topic inserted at front of list
- last row is the add-new-subscription input + "+" icon button
- unsubscribe uses "×" (Close) icon only, no text
- subscribe uses "+" (Add) icon only, no text
- clicking a topic text copies it to clipboard via navigator.clipboard

### Explorer panel
- removed the "Refresh" button (live pane, no manual refresh needed)
- first level of tree nodes expanded by default
- expand/collapse button also recursively expands/collapses all tree nodes
- values displayed right-aligned in monospace font (min-width: 100px)
- clicking any row copies the full topic path to clipboard
- leaf value rows have a "+" button to add a MudNodeModel widget to the diagram with topic and name pre-filled

### Log card (previously "Recent Messages")
- renamed to "Log"
- MudTable grid view: Time | Topic | Value columns, one row per message
- compact single-line display (Dense=true)
- timestamp formatted as HH:mm:ss when today, yyyy-MM-dd HH:mm:ss otherwise
- shows last 20 messages ordered newest-first

## Home (Diagram display page)
- nodes are locked (Locked=true) — no moving, deleting, or keyboard interaction
- data widget no longer shows inline topic or last-updated time
- topic and last-updated displayed in a MudTooltip on hover over the value

## Diagram Edit page
- data widget tooltip same as above (shared MudNodeWidget.razor)
- "Edit Node Properties" button only enabled when exactly one node is selected (disabled for 0 or 2+ nodes)

## Logging
- `MqttDashboard.Server.Services.MqttClientService` per-message log calls changed from `LogInformation` to `LogTrace`
- `appsettings.Development.json` updated to set `MqttDashboard.Server.Services.MqttClientService` log level to `Trace`
