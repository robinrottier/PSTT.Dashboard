
Proposal for a table widget

DIsplay using MudTable for consitent look accross other dashboard widgets.

Row/column setup may be too complicated for the properties dialog, so maybe its a lit of definitions 
for setting up the table. Properties might be things like:
- table
- rowtemplate
- columntemplate
- headerrowtemplate
- celltemplate
- row
- column

So "properties" editor has a listbox with text strings to be edited each of which beng a json format

At least then the label of each row gives a clue as to it purpose and heptext or examples would show format.

Each individual setting is a json string defining the properties for that level, e.g. row template might be something like:
{
  "fontSize": "12px",
  "fontColor": "#000000",
  "backgroundColor": "#ffffff",
  "textAlignment": "left",
  "border": {
	"thickness": "1px",
	"color": "#000000"
  }
}

There ought to be some overlap with simple features of the properties dialog and maybe this need refactoring
e.g. fnt size, colour, background are a common group of properties accross all wodget and now individul components inside a widgets so some sort
or shared properties edito for that

All settings are optional and it shoul dbe able to get a visible usefl table widget very easilly with just a data topic

Nesting would reflect table structure, e.g. table has rows and rows have cells, etc. (like html table etc)

Properties at top level (i.e. table) affect everything unless overriden at lower levels, e.g. row and cell properties
table->row->column->cell (ie. cell settings override column settings etc)

We will want visual aspects to be controllable (at each level):
- font size
- font color
- background color
- text alignment
- borders, thickness, color
- maybe even conditional formatting based on data values, e.g. if value is above a certain threshold then background color is red, etc. which would mirror the color transition config fomr other widget in the package.

Column widths too - maybe fixed or maybe dynamic based on content,
e.g. if content is wider than column width then it wraps to multiple lines and row height increases accordingly
or column width expands to fit (but also fit whole table inside widget size)
Dont want too much contant auto sizing and colums changing


Data mapping needed to be supported:

1. Per cell
- each cell is either simple text or has a data topic and updates accordingly -- same as basic text node maybe a single format string covers both..."value is {0:0}"
- so maybe i this case cells can have mutliple data items as per other nodes
- tble size is then fixed to what is defined

2. Per row
- each row is a data topic (likely a wildcard) and cells update with a data value from a child of that topic
- e.q. row topic is "sensors/room1/+" and cells are "temperature" and "humidity" and update with the value of
- some sort of column defintiion defines the data value for each cell in that column, e.g. "temperature"

3. Per table
- the table is a data topic (likely a wildcard) and rows and cells update with data values from that topic
- e.g. table topic is "sensors/+/+" and rows are room1, room2, etc. matching the first wildcard "+" and cells are temperature and humidity matching the second wildcard "+"
- some sort of column definition defines the data value for each cell in that column, e.g. "temperature" and "humidity"
- in this case the table size is dynamic and changes as rows are added and removed based on the data topic
- OR rows coul dbe fixed to preselected topics and only cells update with data values from those topics, e.g. row 1 is "sensors/room1/+" and row 2 is "sensors/room2/+" and cells are "temperature" and "humidity" and update with the value of those topics
- in this case the table size is fixed to what is defined but data values update based on the data topic


--> Maybe each element (table, row, cell) has a set of string properties which can then be used with some sort of substituition syntax 
in the values of other properties, e.g. cell data topic is "sensors/{row}/temperature" where row is the row topic defined at row level
or table defines data topic as "sensors/{row}/{column}" and each cell gts that corresponding subscription
-- that avoids using wildcards in the data topic and allows more control over the table size and structure while still allowing dynamic data updates based on the data topic	


Nested widgets
- we will want to be able to layout child widgets, especially buttons, text entry etc inside cells, so they benefit from alignment and layout
- how will these nested widget work out their data topic? they may need to inherit some settings from the row or column
  e.g. their data topic is "sensors/{row}/button" where row is "room1" defined at the row level

Inversion
- maybe we want to be able to invert the table so rows become columns and columns become rows,

Responsive layout
- below a certain width the table layout changes to a more mobile friendly format, e.g. each row becomes a card with the column headers as labels and cell values as values in the card, etc.

