# TODO

_Completed items are recorded in [CHANGELOG.md](CHANGELOG.md)._

---

## BUGS

- [ ] Data explorer dialog -- tree view of all available data
	- [ ] can this dialog be resizeable and remember is position
	- [ ] Can there be mutliple topics (just like in node properties) or a comma sep list might be easier.

## 🟡 Minor Enhancements

- [ ] release.ps1
	- [ ] Can the captured output be scrolled to UI on a single line so user sees something happening during the build process? Currently it just looks like nothing is happening for a long time until the build finishes and then the tail of the output is shown.
    - [ ] if a step is stuck on a command prompt for input can anything be done to detect that and abort or prompt user?
	- [ ] detailed output review on failure — "Show detailed" option at failure prompt to re-display full captured output (beyond the 50-line tail shown automatically)
	- [ ] dep check at menu could also transitively resolve (e.g. selecting `tag` + `changelog` without `version` — currently only direct deps are added)
- [ ] Need a way to share dashboards between installations (and dev). Can the API be opened up with a read/write interface to other isntallations via https??
	- [ ] Then in "OPen" and "Save As" dialogs we could choose destaniotn respository: local file or remote dashboard repo (with list of dashboards to choose from)
- [ ] Serialization:
	- [ ] logged-on user not yet written to `FileInfo` (always admin for now — fine to leave)
	- [ ] should include version of this app doing the write, and server written from

- [ ] Node Property dialog - color transition
	- [ ] Needs a means to drag reordering around the conditions to specify which is first match
- [ ] Data item topics per node
	- [ ] "Link animation" needs a property for index of which data item to animate upon
- [ ] Page tabs
	- [ ] Use MudTabs and related controls for displaying. MudTabs has a different model...every page is rendered inside tab component BUT maybe there's a way to use index of selected tab to render it outside MudTabs component?
	- [ ] Position option for tabs: top/left/right/bottom in dashboard properties
	- [ ] Drag to reorder pages when in edit mode. MudTabs would support this but need setting noticed and saved.
- [ ] Node properties dialog
	- [ ] Can this dialog be moveable and have apply button to changes dynamically without closing

- [ ] Log viewer columns: choices for date (and format), time (and format), topic path, topic name, topic full path&name, value — **Full 6-column boolean options done**; date/time format options still open
- [ ] IMport and Export dont seem to be able to see Windows clipboard ... is there some permissions to enable it? This was on firefox

- [ ] Serialization: node ID GUIDs in file — map to sequential 1-based IDs for file (need port+link ID remapping too). Needs a json serilaizer class for Dashboard to manage the mapping.


## 🟡 Features

### FEAT-A: MQTT topic wildcards per node
- [ ] Allow node `DataTopic` to use `#` / `+` wildcards (e.g. `home/sensors/#`)
- [ ] In node text, use named substitution syntax like `{power}` where the key is the trailing topic segment
- [ ] `MqttTopicSubscriptionManager` already handles wildcard routing server-side; extend client binding

### FEAT-B: MQTT data processing / calculated values
- [ ] Support simple expressions/transforms on incoming values before display (unit conversion, arithmetic, string concat)
- [ ] Option: "virtual topics" defined at dashboard level, computed from raw MQTT values, reusable across nodes
- [ ] Option to write calculated values back to the MQTT broker

### FEAT-C: Additional node types _(Gauge, Switch, Battery, Log, TreeView done — see CHANGELOG)_
- [ ] **Text node** - different node shapes (circle, diamond, etc.) Perhaps the "shapre" applies to all derived
      nodes too e.g. a guage inside a triangle or circle. Or maybe shape is just a property of the base node.
- [ ] **Guage**
	- [ ] needs alternatives such as full circle, 90 or 270 .... maybe thats all the 
		  option is, how much of a circle is drawn and properties to control orientation
	- [ ] options to draw "needle" also from some center point to the guage ...	  
- [ ] **Markdown / HTML** — formatted static content, optionally with data substitution
- [ ] **IFrame** — embed another web page
- [ ] **Chart** — in-memory time-series sparkline graph

### FEAT-D: Multiple dashboard pages _(basic multi-page done — see CHANGELOG)_
- [ ] Page tab overflow handling (scrolling/dropdown when many pages)
- [ ] Swipe left/right gesture on mobile
- [ ] Page reordering (drag tabs)

### FEAT-E: Editing improvements
- [ ] Add node dialog
	- [ ] drag onto the canvas and positioned without loosing the dialog
- [ ] Keyboard funcionality:
	- [ ] arrows to move selcted nodes
- [ ] Data explorer dialog -- tree view of all available data
	- [ ] drag a data item to an existing node to add it to that node
	- [ ] can this dialog be resizeable and remember is position
	- [ ] Can there be mutliple topics (just like in node properties) or a comma sep list might be easier.

### FEAT-F: Link improvements
- [ ] Links as proper model objects with a properties editor: color, thickness, dash style
- [ ] Arrow heads to show flow direction
- [ ] Data-driven link styling — color/intensity driven by a topic value
- [ ] Draggable Bezier control points
- [ ] Fork/junction points between links

### FEAT-G: Grouping / layout containers
- [ ] "Group" box — labeled background rectangle that visually wraps related nodes
- [ ] Moving a group moves all contained nodes
- [ ] Split panel type controls to divide up work area into resizable sections
- [ ] Nodes can have width/height of 100% (or "dock" options as in previous vb and win forms)

### FEAT-H: Data layer refactor _(Phases 1–4 complete — see CHANGELOG)_
- [ ] Lazy cache/Grace period: if last client unsubscribes from a topic, keep the server-side broker subscription alive for a configurable delay (e.g. 30 s) before actually unsubscribing from the broker — avoids churn if a circuit reconnects ✅ done

**Phase X — Plugin / alternate data sources**
- [ ] Extend plugin architecture for data sources beyond MQTT
	- [ ] Built-in integrations: REST APIs, WebSockets, Home Assistant local API, Emoncms feeds and time-series
	- [ ] Mock data generator server implementation (useful for testing / demo without a broker)
	- [ ] Finance market plugin (e..g Yaho finance?)
- [ ] Admin configures available plugins; nodes select source and configure connection

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
- [ ] More automation to speed release process
	- [ ] Local script to run tests, do a final commit, and push, create PR, let the various actions run, merge the PR to main and kick patch-release. Then kick deployment to test server
	      release.ps1 now covers this flow, including automated submodule branch management

### FEAT-M: Settings persistence _(done — settings now in data directory)_

### FEAT-N: Self-updating deployment
- [ ] The Latest version check checks for tags ... but the actual Docker image may not be available for some time after a tag is pushed. Can it check actual images in ghcr?
- [ ] Option to follow release-only stream or latest beta stream of pre-releases
- [ ] How would we revert to a previous version if an update proved bad?


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
 
