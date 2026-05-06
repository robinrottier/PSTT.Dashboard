# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS

## 🟡 Minor Enhancements

- [ ] release.ps1
	- [ ] if a step is stuck on a command prompt for input can anything be done to detect that and abort or prompt user?

- [ ] Node Property dialog - color transition
	- [ ] Needs a means to drag reordering around the conditions to specify which is first match

- [ ] Data item topics per node
	- [ ] "Link animation" needs a property for index of which data item to animate upon

- [ ] Page tabs
	- [ ] Use MudTabs and related controls for displaying. MudTabs has a different model...every page is rendered inside tab component BUT maybe there's a way to use index of selected tab to render it outside MudTabs component?
	- [ ] Position option for tabs: top/left/right/bottom in dashboard properties
	- [ ] Drag to reorder pages when in edit mode. MudTabs would support this but need setting noticed and saved.

- [ ] Log viewer columns: choices for date (and format), time (and format), topic path, topic name, topic full path&name, value — **Full 6-column boolean options done**; date/time format options still open
- [ ] log viewer colum width: column width changeable via mouse drag and saved in properties _(configurable pixel widths per column added; mouse resize still TODO)_

- [ ] IMport and Export dont seem to be able to see Windows clipboard ... is there some permissions to enable it? This was on firefox

- [ ] testing
	- [ ] add 'SubscribeAsync' test helper on RemoteCache/RemoteCacheClient to await initial non-pending delivery (tests only) — avoid timing races on slow CI agents
	- [ ] add integration test harness helper to wait for server-side cache retention (GetSnapshot/GetCounts) before subscribing, for CI robustness



## 🟡 Features

### FEAT-A: MQTT topic wildcards per node
- [ ] Allow node `DataTopic` to use `#` / `+` wildcards (e.g. `home/sensors/#`)
- [ ] In node text, use named substitution syntax like `{power}` where the key is the trailing topic segment

### FEAT-B: MQTT data processing / calculated values
- [ ] Support simple expressions/transforms on incoming values before display (unit conversion, arithmetic, string concat)
- [ ] Option: "virtual topics" defined at dashboard level, computed from raw MQTT values, reusable across nodes
- [ ] Option to write calculated values back to the MQTT broker

### FEAT-C: Additional node types and dashboard improvements
- [ ] **Text node** - different node shapes (circle, diamond, etc.) Perhaps the "shapre" applies to all derived
      nodes too e.g. a guage inside a triangle or circle. Or maybe shape is just a property of the base node.
- [ ] **Guage**
	- [ ] needs alternatives such as full circle, 90 or 270 .... maybe thats all the 
		  option is, how much of a circle is drawn and properties to control orientation
	- [ ] options to draw "needle" also from some center point to the guage ...	  
- [x] **Grid/Table node** — ✓ MVP complete (Session 1). Remaining sessions:
      - Session 2: JsonEditorField reusable component; full ColumnDefs/RowDefs/CellDefs with formatting; PerCell improvements
      - Session 3: Hierarchical styles + conditional cell formatting
      - Session 4: PerRow mode, Invert, responsive card-stack layout
      - Session 5: Nested widget cells (Button, Switch, TextEntry) via DynamicComponent with {row}/{col} substitution
	  - Current highlighing implementatin needs to be optional and configurable
	  - Rows (and Col) definition need a template or something to set optional defaults for each actual item

- [ ] **Text entry**
	- [ ] Support text format for entry: string, date, time, datetime, numeric int/float/%age/
	- [ ] validation: string length, reg ex match, numeric limits etc
- [ ] **Radio group** — similar to button group but with exclusive selection -- **DONE** ✓
- [ ] **Checkbox group** — similar to button group but with independent selection -- just a "visualization display" option as logic the same for all these
- [ ] Anything mudblazor offers for input should be easy to do

- [ ] **Chart**
      — in-memory time-series sparkline graph. Difficult!! where does it get history from?
      - Self collected easy to do but little application if looses it every refresh.

- [ ] Widget libraries- What libraries or packages are availble (FOSS) that would enhance the package?? Are there any emerging standards or widely used packages?
- [ ] Generic property handling for data value transition:
- [ ] Any widget property should be able to have a data transition appliled to it in some generic way
      rather than present where just fixed (typlically color) property has data vaue transition built in only
	  - other use cases would be whole widget visibility based on a data vaue (e.g. hide if 0)
	  - speed of animation of line based on data vaue
	  - 

### FEAT-D: Multiple dashboard pages _(basic multi-page done — see CHANGELOG)_
- [ ] Page tab overflow handling (scrolling/dropdown when many pages)
- [ ] Swipe left/right gesture on mobile
- [ ] Page reordering (drag tabs) somewhereor at least a "move left, move right" option 
- [ ] Page in memory persistence? - should pages be held in memory live for rapdi and no-change switching, or should they be reloaded from razor code each time? Maybe an option for this at page level or dashboard level. Default owuld be keep in memory

### FEAT-E: Editing improvements
- [ ] View zoom/unzoom option and scroll bars for panning view.
- [ ] Keyboard funcionality:
	- [ ] arrows to move selcted nodes

- [ ] Add node panel
	- [ ] drag onto the canvas and positioned without loosing the dialog
- [ ] Data explorer panel
	- [ ] drag a data item to an existing node to add it to that node

- [ ] all the edit mode panels could be combined into a tabbed super panel with a tab for each of these functions (add node, edit node, data explorer, page properties, dashboard properties, etc)
	- [ ] similar to current floating panels but also could be "docked" to right hand side of screen
	- [ ] that is resizeable (and saves size and position and dock state i,e. floating or ocked)
	- [ ] and when in edit mode this panel is shown by default but can be hidden to give more canvas space, and then shown again when needed
	- [ ] whole thing is shown/hidden via single button
	- [ ] thing becomes very extendable
	- [ ] contained panels:
		- [ ] nodeproperties panel is simple list of all properties and a simple editing value
		- [ ] second propeties panel (ie. current dialog) would be for specialized node properties with the layout and cusom dialog as present
		- [ ] dashboard oroperties and page properties another panel - again have linear list or ghioh value custom dialog
		- [ ] add node
		- [ ] data explorer
		- [ ] ...so maybe tool bar olong top to choose which panel (node, page, table, data, add node) but also for some panel type a binary choice of list or custom
	- [ ] rather like the node-red editing page with slide out panel on the right side

### FEAT-F: Link improvements
- [ ] Links as proper model objects with a properties editor: color, thickness, dash style
	- [ ] Data-driven link styling — color/intensity driven by a topic value
	- [ ] remove link animation from current widget joined to the link and build it into the link itself 
- [ ] POrt options:
	- [ ] Arrow heads to show flow direction
	- [ ] Not at all or single ended - or much finer than current black blob.
	- [ ] Diffenent view in edit mode as has to be visible
- [ ] Link port to centre of widget aswell as edge ports (blozor diagrams supports this).
- [ ] Mutiple edge ports per side spaced properly or on top of each other? (implement in blazor diagrams)
- [ ] Draggable Bezier control points
- [ ] Fork/junction points between links

### FEAT-G: Grouping / layout containers
- [ ] "Group" box — labeled background rectangle that visually wraps related nodes
- [ ] Moving a group moves all contained nodes
- [ ] Split panel type controls to divide up work area into resizable sections
- [ ] Nodes can have width/height of 100% (or "dock" options as in previous vb and win forms)

### FEAT-H: Data layer refactor
- [ ] `PSTT.Remote.Sub` (`pstt-sub`) CLI tool
	- [ ] **`pstt-monitor`** — interactive TUI using [Terminal.Gui v2](https://github.com/gui-cs/Terminal.Gui): tree of topics on the left panel (keyboard-navigable expand/collapse), live value + history on the right. Similar to `dbus-monitor`. Separate exe in `libs/PSTT/src/PSTT.Remote.Monitor/`.
- [ ] Create a simple (Windows) app using tree view control to make a request and show content
	  -- in fact this would be a data explorer app (without buttons and edit mode features)

### FEAT-I: Responsive / mobile layout
- [ ] Responsive layout adapts to screen size
- [ ] Per-page optional alternative layout for mobile
- [ ] Touch-friendly editing interactions

### FEAT-J: User management & auth
- [ ] Multi-user with roles: read-only / read-write / admin
- [ ] Builds on existing `ServerAuthService` / admin password hash mechanism
- [ ] User registration mechanism (username, email, password) with email verification--not sure how as this deployment wont have a mail?
- [ ] "Admin" is a status now and not an actual user name. Mutliple users could log on and be "admin"
	- [ ] Admin user can create other users, assign roles, and delete users
	- [ ] First time setup requires creating an admin user, which is then used to log in and manage the system
- [ ] COnfirmation of each new user registration from admin user
	- [ ] - new users stays "pendign" until admin confirms request
	- [ ] when admin logs on they see pending user registrations and can confirm or reject them
- [ ] User management UI in admin interface
- [ ] Usr "database" is nothign complicated -- fine as an encryted file store and password encrypted in that
- [ ] JWT-based auth for API endpoints, with token issued on login and stored in browser local storage
	- [ ] I dont know what that means -- does it apply to this type of setup? API calls are all internal frmo client to server

### FEAT-K: Dashboard versioning & Git integration
- [ ] Optional Git commit/push of dashboard to a remote repo
	- [ ] Modeled on node-red project feature.
- [ ] Or history/backup for dashboards (previous versions, compare/diff) for non-git 

### FEAT-L: Deployment enhancements
- [ ] Admin interface: runtime monitoring, logs, connected clients, dashboard file management
- [ ] We've lost the "restart" button ...might want to do a restart for other reasosns
- [ ] Backend (docker) deployment of a new version shoul dbe detected by front end and a prompt to restart/reload offered
- [ ] If backend has become incompatible (version change etc) then restart coul dbe forced

### FEAT-M: Settings persistence _(done — settings now in data directory)_

### FEAT-N: Self-updating deployment
- [ ] The Latest version check checks for tags ... but the actual Docker image may not be available for some time after a tag is pushed. Can it check actual images in ghcr?
- [ ] Option to follow release-only stream or latest beta stream of pre-releases
- [ ] How would we revert to a previous version if an update proved bad?

### FEAT-O: PLugin data enhaancments
- [ ] Extensible plugin architecture for data sources beyond MQTT
	- [ ] Build integrations for:
		- [ ] REST APIs
		- [ ] WebSockets
		- [ ] Home Assistant local API,
		- [ ] Emoncms feeds and time-series
		- [ ] Yahoo finance
		- [ ] Public Waether APIs
		- [ ] Publis solar forecast APIs
		- [ ] 
	- [ ] Mock data generator server implementation (useful for testing / demo without a broker)
	- [ ] Finance market plugin (e..g Yaho finance?)
- [ ] Admin configures available plugins; nodes select source and configure connection



---

## 🟢 Chores / Cleanup

- [ ] **CHORE-1** — Remove unused overloads, components, classes, `using` directives
- [ ] **CHORE-2** — Add XML doc comments to key services and models (especially MQTT handling, SignalR dual-path)
- [ ] **CHORE-3** — Expand test coverage:
  - Unit: MQTT message handling, SignalR hub, `MqttTopicSubscriptionManager`
  - Integration/system:
	- full flow with headless browser (Playwright?),
	- a console based "client" that connects to the server and validates:
		- the SignalR messages received for a given dashboard file and set of MQTT messages published?
		- This would be a more lightweight test than spinning up a full browser instance, and could run as part of the regular test suite.
		- Or testing functions via some sort of "command interface" simulating menu selections etc
	- both WASM and Server-only modes
 
