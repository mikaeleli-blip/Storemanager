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
5. **Grow one store, gradually.** Start as a small corner shop with a few
   basic groceries. Sales earn XP; store levels and money unlock licences for
   new products and departments, new staff roles and bigger premises.

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

### Office computer
- Walk into your office and press **Tab** (or the "Sit at desk" button). The
  camera moves onto the monitor and the **office computer** fills the screen,
  like the PC in Supermarket Simulator.
- The desktop has app icons, draggable windows and a taskbar (get up, build,
  open apps, game speed, clock). Widgets on the right show today's numbers,
  level progress, alerts and the latest messages.
- Apps, all only here: **Shop** (order stock), **Bills** (supplier invoices),
  **Products** (prices, stock and expiry), **Licences**, **Staff**, **Hiring**,
  **Assistant** (delegation), **Stats** (today and the last 7 days),
  **Expansion**, **Inbox** (all messages).
- **Build** leaves the screen for the overhead camera: **WASD** pans over the
  whole store, Q/E rotate, wheel zooms; place fixtures and pick shelf products.
  **Esc** goes back to the computer.
- Esc on the computer closes the front window. **Tab** gets up and walks the
  floor.

## Progression: levels and licences

- Every customer who pays earns **XP** (2 + 1 per $5 in their basket). XP
  raises the **store level** (1–12); the top bar shows level and progress.
- **Licences** (Licences tab) unlock what you may sell. Each needs a store
  level and costs money; some need another licence first.
- **Department licences** unlock a new fixture type plus its first products
  (and a staff role where needed). Later licences add more products to it.
- You start with **Basic Groceries** (pasta, canned soup, two colas,
  snacks): gondola shelves and checkouts only.

| Licence | Level | Cost | Unlocks |
|---|---|---|---|
| Basic Groceries | 1 | free | Pasta, Canned Soup, Bubbla Cola, Zing Cola, Snacks; shelves, checkouts |
| Pantry Staples | 2 | $1,200 | Rice, Cereal, Sliced Bread |
| Produce Department | 2 | $2,500 | Produce tables; Apples, Bananas, Potatoes |
| Coffee | 3 | $1,500 | Coffee |
| Dairy Department | 3 | $4,000 | Fridges; Milk, Eggs, Yogurt |
| Salad Vegetables | 4 | $1,800 | Tomatoes, Lettuce (needs Produce) |
| Household Department | 4 | $2,500 | Toilet Paper, Dish Soap |
| Frozen Food | 5 | $3,500 | Frozen Pizza, Ice Cream (needs Dairy) |
| Cheese | 5 | $1,500 | Cheese (needs Dairy) |
| Bakery Department | 6 | $8,000 | Oven, bakery shelves, bakers; Fresh Bread, Croissants |
| Cleaning & Batteries | 6 | $2,000 | Detergent, Batteries (needs Household) |
| Deli Department | 7 | $12,000 | Deli counters, deli clerks; Sliced Ham, Chicken |
| Cakes | 8 | $3,000 | Cakes (needs Bakery) |
| Deli Salads | 9 | $3,000 | Salads (needs Deli) |
| Butcher's Cuts | 10 | $6,000 | Steak (needs Deli) |

- **Levels also gate:** cleaners (level 2), the assistant manager (level 3),
  and each **expansion** (levels 3, 5, 7, 9, 11).
- Customers mostly want what you sell, but some ask for things you don't
  ("Asked for but not sold here" on the Overview): a hint for what to license
  next.

## Building the store

The game starts with an **empty corner shop** (24 m wide): walls, entrance, office, back room
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
- **Expansion** adds 12 m of shop floor and 2,000 units of back room
  (finished at 06:00 the next morning). Needs the store level, costs $8,000
  + $6,000 per earlier expansion; rent goes up $175/day each time.

## Products and prices

- Six categories: Grocery, Household (shelves), Produce (tables), Dairy &
  Frozen (fridges), Bakery (bakery shelves, baked on site), Deli & Meat
  (counters).
- Each category has a **price level** (80%–150% of base). Higher prices mean
  more profit per item but fewer customers buy, and satisfaction drops.
- Stock lives on the fixtures and in the **back room**. Stockers move it from
  the back to the emptiest shelves. Perishables age each night and expire.
- **Ordering is a shopping cart** (Shop app): the catalogue lists every
  licensed product by supplier, with case size, case price, shelf life, stock
  (shelf, back room, on the way) and average daily sales. Add cases, then
  order: each supplier delivers on its own next delivery day at 06:00, or
  **express** in about an hour for 25% more (handy for opening day).
- The cart can't be bigger than the free back-room space (stock already on
  the way counts). Weekends are busier (Fri/Sat) than Sun-Tue.

## Suppliers, bills and expiry

| Supplier | Sells | Delivers | Bills due | Min order | Expired stock back |
|---|---|---|---|---|---|
| Central Warehouse | dry goods, produce, frozen, household | every day | 10 days | - | no credit |
| Bubbla Beverages | Bubbla Cola | Mon, Wed, Fri | 14 days | $100 | 100% credit |
| Zing Drinks | Zing Cola | Tue, Thu, Sat | 14 days | $100 | 100% credit |
| Valley Dairy Co-op | milk, eggs, yogurt, cheese | Mon-Sat | 7 days | $60 | 75% credit |
| Millhouse Bakery | sliced bread | every day | 7 days | $40 | 100% credit |
| Fjord Meats | deli meats, salads | Mon, Thu | 7 days | $150 | 50% credit |

- **Bills (reikningar):** every delivery comes with an invoice; nothing is
  paid when ordering. Pay them in the Bills app (one by one, or everything due
  within 3 days). Overdue: a 5% late fee; 7 days overdue: the supplier stops
  taking orders until paid. Each supplier also has a **credit limit** (what
  you may owe, including orders on the way), which grows with store level.
  The assistant manager can be delegated to pay bills due today or tomorrow.
- **Expiry:** stock arrives with a best-before date (often with part of its
  shelf life already used). Expired stock **stays on the shelf** until staff
  scan it out. Customers who pick up an expired item get upset and may
  complain; they never buy it. A quarter of shoppers take the freshest date
  from the back, which leaves older stock to expire.
- **Date checks:** stockers pull expired stock from every shelf they refill
  and from the back room. Staff app setting: only when restocking, a date-check
  round once a day (before opening if there's a night stocker), or every
  shift. Rounds cover slow sellers too but take time from restocking. Bakers
  clear old bakery stock when they put out a fresh batch.
- **Returns (endursending):** scanned-out stock from a supplier with a returns
  policy goes back with that supplier's next delivery, and the credit comes
  off their next bill. The in-store bakery and the warehouse give nothing back.
- In headless runs a well-run store wastes little: most perishables sell
  within a day or two. Waste shows up in the bakery, on meat (deliveries only
  Mon and Thu) and when you over-order.

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

- **Ordering:** an hour before their shift ends, orders everything on sale
  to last until each supplier's delivery after next, within back-room space,
  credit limits and minimum orders. Low leadership means less accurate orders.
- **Bills:** pays supplier bills due today or tomorrow (keeps $300 in the
  till).
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
- **Orders** arrive with each supplier's truck at 06:00 (or express) and are
  billed on delivery (see Suppliers, bills and expiry). The back room holds
  5,000 units (+2,000 per expansion). If a delivery still doesn't fit (for
  example after clearing shelves into the back), every product gets a fair
  share and the rest goes back on the truck (not billed).
- Rent is charged every midnight and grows with expansion.
- Start with $20,000, a cashier for each shift and a morning stocker. Rent
  $350/day. A starter shop (2 checkouts, 6 shelves) plus express opening stock
  costs about $9k. In the headless auto-player run, sales grow from ~$3k a day
  to ~$13k a day by day 30, with level 12 around day 30–35 and the big
  departments (bakery, deli) bought as money allows.

## Time

- 1 second at 1x = 30 game seconds. Open 08:00–22:00.
- Speeds: pause, 1x, 2x, 4x, 8x. While the store is closed, time runs 4x
  faster (can be turned off).
- No save system yet: playtests in Unity, where pausing serves as a save.

## Roadmap

1. **First slice (this build):** build mode with free placement, one product
   per shelf, licences and store levels, staff roles and shifts, assistant
   manager, customers, orders, express delivery, expansion, office/floor.
2. Staff breaks, break room, overtime, part-timers, weekly schedule screen.
3. Suppliers with different prices and lead times; promotions and sales.
4. Decor and signs you can place; drag-to-place rows of shelves.
5. Self-checkouts, security/shoplifting, events (holidays, heat waves).
6. Real UI (UI Toolkit) replacing the IMGUI debug HUD.
7. Art pass: store interior, characters, animations.
8. Save/load, difficulty and failure settings, more store types.
