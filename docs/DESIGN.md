# Storemanager — Design Document

A management game about running one supermarket. You are the store manager.
You never stack a shelf or run a till yourself. You hire people, put them on
shifts, set up departments, set prices, order stock, appoint an assistant
manager and grow the store.

## Pillars

1. **You manage people, not shelves.** Every shelf that gets filled and every
   customer that gets served is done by a member of staff.
2. **The store is a living system.** Customers, staff, stock and money are
   simulated one by one. Problems come from the simulation (empty shelves,
   long queues, spills, unhappy staff), not from scripted events.
3. **Floor vs office.** On the floor you are a person: you can only be in one
   place, and being there matters. In the office you see everything and have
   all the tools, but nobody on the floor sees you.
4. **Delegation.** The assistant manager takes routine work off your hands.
   They are only as good as their skills, and only there on their shift.
5. **Grow one store.** Buy more floor space, add departments, add checkouts,
   get a bigger back room.

## Platform and presentation

- Unity 6 (6000.6.5f1), 3D top-down / isometric.
- The current build is a **grey-box**: coloured primitives, procedural store
  layout, IMGUI debug UI. Systems first, art later.
- Store type: **supermarket**. Categories, products and fixtures are data
  (`Catalog.cs`), so other store types can be added without touching the
  simulation.

## The player

### Floor mode (default)
- You control the manager character: **WASD** to walk (camera-relative) or
  **right-click** to walk to a point. Camera follows. **Q/E** rotate,
  **mouse wheel** zooms.
- **Manager presence:** staff within 6 m work 15% faster and slowly gain
  morale.
- **Talk to staff:** click someone within 3 m: **Praise** (+morale, once per
  day each) or **Warn** (−morale, +20% speed for the rest of the shift).
- **Complaints:** an unhappy customer may ask for the manager (red marker).
  Walk over to resolve it before they give up and leave.
- **Fixtures:** click a shelf to see what it holds and how full it is.

### Office mode
- Walk into your office and press **Tab** (or the "Sit at desk" button).
- The camera detaches: **WASD** pans over the whole store, Q/E rotate, wheel
  zooms.
- All management screens are here, and only here: Overview, Build, Products,
  Staff, Hire, Delegation, Expansion, Finances.
- Press **Tab** again to get up and walk the floor.

## Building the store

The game starts with an **empty store**: walls, entrance, office, back room
and loading dock. Time is paused until you start it. Everything on the shop
floor is placed by you, from the **office desk** (Build tab):

| Fixture | Size | Cost | Holds |
|---|---|---|---|
| Shelf (gondola) | 1x2 | $800 | Grocery or household, 64 units |
| Fridge | 1x2 | $2,400 | Dairy & frozen, 48 units |
| Produce table | 1x2 | $900 | Fruit & veg, 50 units |
| Bakery shelf | 1x2 | $700 | Baked goods, 36 units (needs an oven + baker) |
| Deli counter | 3x1 | $4,500 | Deli & meat, 40 units (needs a deli clerk) |
| Bakery oven | 2x3 | $6,000 | Where bakers bake |
| Checkout | 1x2 | $1,500 | Till; cashier on one side, queue on the other |

- Pick a fixture, move the mouse: a green/red ghost shows if it fits. **R**
  rotates, left-click places, right-click cancels. Blue squares show where
  staff or customers stand (they must stay free).
- Placement is refused if it would block the entrance, the staff doors, or
  the way to any other fixture.
- **Each shelf unit holds one product, chosen by you.** Click a shelf to pick
  it from the products its type allows. Changing or clearing a shelf sends its
  stock back to the back room.
- Fixtures can be **moved** (keeps its product and stock) or **sold** for 50%.
- Customers go to the fixtures holding what's on their list. Products with no
  shelf count as "things you don't sell".
- **Expansion** adds 12 m of shop floor and more back-room space (finished at
  06:00 the next morning).

## Products and prices

- Six categories: Grocery, Household (shelves), Produce (tables), Dairy &
  Frozen (fridges), Bakery (bakery shelves, baked on site), Deli & Meat
  (counters).
- Each category has a **price level** (80%–150% of base). Higher prices mean
  more profit per item but fewer customers buy, and satisfaction drops.
- Stock lives on the fixtures and in the **back room**. Stockers move it from
  the back to the emptiest shelves. Perishables age each night and expire.
- **Par level** per product = how much to keep on hand. "Order to par" orders
  the difference. Orders come with the 06:00 truck, or by **express** within
  an hour for 25% more (handy for opening day).

## Staff

- Skills (1–10): **Speed**, **Service**, **Reliability**, **Leadership**;
  some have a **specialty** (Baking, Butchery).
- **Roles:** Cashier, Stocker (restocks everything or one category), Cleaner,
  Baker, Deli Clerk (serves whichever counter has people waiting), Assistant
  Manager (one only).
- **Shifts:** Morning 07–15, Evening 14–22, Night 22–06 (+25% pay). Night
  stockers fill shelves while the store is closed.
- **Morale** (0–100) changes with pay vs asking wage, workload, praise and
  warnings. Low morale means slower work, more sick days and quitting. Each
  employee shows why their morale moved last shift (overworked, underpaid...).
- Absences: chance to call in sick depends on reliability and morale.
- Cashiers close their lane at shift end and serve the queue that's left
  before going home.
- Hiring: a pool of applicants that changes daily. Firing lowers everyone's
  morale a little.

## Assistant manager

One employee can be the assistant manager. While on shift they walk the
store (staff near them work 5% faster) and do the jobs you delegate:

- **Ordering:** orders everything on sale up to par an hour before their
  shift ends. Low leadership means less accurate orders.
- **Complaints:** walks over to complaining customers (less effective than
  you).
- **Checkouts:** jumps on a free till when queues get long.
- **Staff morale:** small morale boost to staff they pass.

## Customers

- Arrive by time of day (lunch and after-work peaks) × reputation × how much
  of what people want you sell.
- Each has a shopping list of 3–10 items drawn from **all** products, not
  just the ones you sell. They walk to the nearest stocked fixture for each.
- Satisfaction (starts at 70) goes up and down with: items found / out of
  stock / not carried, prices, queue time, no open checkout, spills nearby,
  cashier service, and complaints being resolved.
- Each night reputation moves towards the day's average satisfaction.
- The Overview screen shows **what upset customers today** (empty shelves,
  queues, prices, spills, things you don't sell...), so problems are traceable.

## Economy

- Cash, daily ledger: sales, wholesale, wages, rent, construction, waste.
- **Orders** arrive by truck at 06:00 and are paid on delivery. Back room has a
  capacity limit.
- Rent is charged every midnight and grows with expansion.
- Start with $60,000. A sensible opening store (3 checkouts, ~12 shelves,
  produce and fridges) plus opening stock costs about $45k and makes roughly
  $3,000 a day (headless simulation). Never ordering empties the shelves fast.

## Time

- 1 second at 1x = 30 game seconds. Open 08:00–22:00.
- Speeds: pause, 1x, 2x, 4x, 8x. While the store is closed, time runs 4x
  faster (can be turned off).
- No save system yet: playtests in Unity, where pausing serves as a save.

## Roadmap

1. **First slice (this build):** build mode with free placement, one product
   per shelf, staff roles and shifts, assistant manager, customers, orders,
   express delivery, expansion, office/floor.
2. Staff breaks, break room, overtime, part-timers, weekly schedule screen.
3. Suppliers with different prices and lead times; promotions and sales.
4. Decor and signs you can place; drag-to-place rows of shelves.
5. Self-checkouts, security/shoplifting, events (holidays, heat waves).
6. Real UI (UI Toolkit) replacing the IMGUI debug HUD.
7. Art pass: store interior, characters, animations.
8. Save/load, difficulty and failure settings, more store types.
