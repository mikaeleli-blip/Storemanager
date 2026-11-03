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
- Store type: **supermarket**. Departments and products are data
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
- **Department panel:** standing in a department shows its live stats.

### Office mode
- Walk into your office and press **Tab** (or the "Sit at desk" button).
- The camera detaches: **WASD** pans over the whole store, Q/E rotate, wheel
  zooms.
- All management screens are here, and only here: Overview, Staff, Hire,
  Departments, Stock & Orders, Delegation, Expansion, Finances.
- Press **Tab** again to get up and walk the floor.

## Store layout

Grid of 1 m tiles. From the street inwards:

| Rows (z) | Area |
|---|---|
| 0–2 | Street (customers arrive and leave) |
| 3 | Front wall with the entrance |
| 4–9 | Lobby and checkout lanes |
| 10–11 | Front cross aisle |
| 12–25 | Department slots, 12 m wide each |
| 26 | Back wall with staff doorways |
| 27–33 | Back room (stock racks) and the manager's office |
| 34 | Rear wall with the loading dock door |
| 35 | Dock / staff entrance |

- The store starts with **4 slots**: Grocery, Produce, Dairy & Frozen, and one
  empty slot.
- **Expansion** adds one slot (12 m) to the right, one more checkout lane and
  more back-room space. Max 7 slots. Construction finishes at 06:00 the next
  morning.

## Departments

| Department | Kind | Notes |
|---|---|---|
| Grocery | Shelves | Long shelf life |
| Produce | Shelves | Short shelf life, waste |
| Dairy & Frozen | Shelves | Mixed |
| Household | Shelves | Cheap to build, low demand |
| Bakery | Bakery | Needs a **baker** on shift; baked on site, sells out, 1–3 day life |
| Deli & Meat | Counter | Needs a **deli clerk**; customers queue at the counter, up to 3 clerks serve at once |

- Each department has a **price level** (80%–150% of base). Higher prices mean
  more profit per item but fewer customers buy, and satisfaction drops.
- Customers also want things the store doesn't carry. The Overview shows
  "asked for but not carried" per department, so it's clear what to build
  next.
- Stock lives on the **shelves** (limited) and in the **back room**. Stockers
  move it from back to shelves. Perishables age each night and expire.

## Staff

- Skills (1–10): **Speed**, **Service**, **Reliability**, **Leadership**;
  some have a **specialty** (Baking, Butchery).
- **Roles:** Cashier, Stocker (assigned to a department or "any"), Cleaner,
  Baker, Deli Clerk, Assistant Manager (one only).
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

- **Ordering:** orders every department up to par levels an hour before their
  shift ends. Low leadership means less accurate orders.
- **Complaints:** walks over to complaining customers (less effective than
  you).
- **Checkouts:** jumps on a free till when queues get long.
- **Staff morale:** small morale boost to staff they pass.

## Customers

- Arrive by time of day (lunch and after-work peaks) × reputation × number of
  departments.
- Each has a shopping list of 2–8 items drawn from **all** departments, not
  just the ones you have.
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
- Numbers are tuned against a headless 12-day simulation: the starting store
  makes roughly $1,500 a day; a bakery pays back in about a week. Doing
  nothing (never ordering) empties the shelves by day 3.

## Time

- 1 second at 1x = 30 game seconds. Open 08:00–22:00.
- Speeds: pause, 1x, 2x, 4x, 8x. While the store is closed, time runs 4x
  faster (can be turned off).
- No save system yet: playtests in Unity, where pausing serves as a save.

## Roadmap

1. **First slice (this build):** one store, 6 departments, staff roles and
   shifts, assistant manager, customers, orders, expansion, office/floor.
2. Staff breaks, break room, overtime, part-timers, weekly schedule screen.
3. Suppliers with different prices and lead times; promotions and sales.
4. Store layout editing: place shelves, fridges and checkouts yourself.
5. Self-checkouts, security/shoplifting, events (holidays, heat waves).
6. Real UI (UI Toolkit) replacing the IMGUI debug HUD.
7. Art pass: store interior, characters, animations.
8. Save/load, difficulty and failure settings, more store types.
