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
- Stylized chunky low-poly models made by script in Blender (`Tools/Blender`):
  fixtures, products, decor and people. IMGUI debug UI for now.
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
  **Products** (prices, stock and expiry), **Licences**, **Staff** (profiles,
  reviews, discipline), **Roster**, **Hiring** (interviews), **Assistant**
  (delegation), **Stats** (today and the last 7 days), **Expansion**,
  **Inbox** (all messages).
- **Build** leaves the screen for the overhead camera: **WASD** pans over the
  whole store, Q/E rotate, wheel zooms; place fixtures and pick shelf products.
  **Esc** goes back to the computer.
- Esc on the computer closes the front window. **Tab** gets up and walks the
  floor.

## Starting a game

The office computer asks how to start (no save system yet, so every launch is
a new game):

- **Hire your own team** (recommended): nobody works there yet. Interview and
  hire everyone, set up the roster, build and stock the shop, then press
  **Open the store** (desktop checklist: a checkout, products on shelves,
  stock ordered, a cashier hired). Until then no customers come and no rent is
  charged, but the clock can run, so interviews, references and deliveries can
  take a few days. More people apply before the grand opening (12 instead of 8).
- **Start with a small crew**: two full-time cashiers, a weekday stocker and
  two weekend part-timers already work there; the store is open from day one.
- **Opening day** is modest, it's a corner shop: a hand-painted NOW OPEN banner
  on two poles, a few balloons and a chalkboard sign (taken down after three
  days). Curious neighbours: +40% customers on opening day, +20% the day after,
  +10% the third day. First impressions count double for reputation, and the
  next morning the local paper writes a few lines about how it went.

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
| Frozen Food | 5 | $3,500 | Chest freezers; Frozen Pizza, Ice Cream (needs Dairy) |
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
| Fridge | 1x2 | $2,400 | Dairy, 48 units (double-sided, glass doors) |
| Chest freezer | 1x2 | $2,600 | Frozen food, 40 units (island freezer, sliding glass lids) |
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
- **What you see is what's there:** every unit on a shelf is drawn, so one
  item looks like one item. How many fit depends on the product's size: a
  2 m gondola holds about 350 pasta bags but 48 toilet-paper packs, a fridge
  700 cartons of milk, a chest freezer 72 pizzas (`Sim/ShelfRows.cs`, made by
  the Blender build from the same shelf layouts the game draws). Bakers still
  bake for the day (about 25 of a product per shelf), not to fill the display.
- Suggested order levels keep long-life products at least half a shelf deep,
  so shelves don't look bare; perishables are ordered by expected sales only.
- Fixtures can be **moved** (keeps its product and stock) or **sold** for 50%.
- Customers go to the fixtures holding what's on their list. Products with no
  shelf count as "things you don't sell".
- **Expansion** adds 12 m of shop floor and 2,000 units of back room
  (finished at 06:00 the next morning). Needs the store level, costs $8,000
  + $6,000 per earlier expansion; rent goes up $175/day each time.

## Products and prices

- Seven categories: Grocery, Household (shelves), Produce (tables), Dairy
  (fridges), Frozen (chest freezers), Bakery (bakery shelves, baked on site),
  Deli & Meat (counters).
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

### Contracts and the roster
- Everyone has a **contract**: full-time (5 shifts a week) or part-time (3).
  Shifts: Morning 07-15, Evening 14-22, Night 22-06 (+25% pay).
- The **Roster app** (vaktaplan) is a Mon-Sun grid per employee: click a cell
  to cycle Off / Morning / Evening / Night. The week repeats; changes to today
  take effect immediately. People's availability is respected (students: no
  weekday mornings, some: no nights or no weekends).
- Shifts beyond the contract are **overtime** (x1.5). Less than **11 hours**
  between two shifts (evening then morning, night then anything next day) means
  tired staff (slower, more sick days, morale down). No day off, or fewer shifts
  than the contract, also costs morale.
- A **coverage** table shows how many of each role are rostered per day and
  shift; red where the store needs someone (cashiers while open, a morning
  stocker). Fridays and Saturdays are busier.
- **Call in** an off-duty employee from their profile when someone's sick
  (overtime if over contract; unhappy on a day off).
- With the small-crew start: two full-time cashiers (weekday mornings and
  evenings), a full-time weekday stocker and two part-time cashiers for
  Fri-Sun. Weekend stocking is a gap to fill.

### Breaks and the time clock
- Every shift has a 30-minute **break** in the office. Breaks are staggered:
  one person per role at a time, and a cashier on the last open till waits up
  to two hours for cover. Two cashiers on a shift keep a till open.
- The **time clock** records late arrivals, no-shows, sick calls and long
  breaks; the till and stock records give output per hour (items scanned,
  units stocked, customers served). Shown per shift in the Staff app.

### Hiring and interviews
- Applicants (a new batch every few days) come with a CV: age, experience,
  last job, contract wish and availability. Their real skills, **work ethic**,
  **ambition** and **charm** are hidden.
- **Interviews** (Hiring app, 4 per day): ask 3 of 8 questions. Each answer
  narrows what you know about a trait. Charming applicants oversell
  themselves. **References** (answer next morning) are honest about
  reliability and work ethic; people with no work history have none.
- **Offer**: role, contract, usual shift and wage. Below their asking wage
  they may say no (and won't consider another offer that day); above it they
  start happier.
- New staff start on **14 days' probation**. You learn their real skills after
  10 shifts.

### Slacking and discipline
- Low work ethic (worse with low morale) means slacking: on the phone,
  chatting, hiding in the back room, smoke breaks, long breaks, turning up
  late. Much less when someone's watching: you on the floor, a shift lead,
  the assistant manager or their department manager. Recent warnings and
  probation also keep people in line.
- **Catch them**: walk within 4 m of someone slacking (you see what they're up
  to from 16 m). It goes on their record.
- **Talk** (on the floor, within 3 m): praise, "a word" (verbal warning:
  behaves for a few days) or a **written warning** (áminning) for a recent
  incident (late, long break, caught slacking, no-show). Written warnings can
  also be given from the Staff app; they stay active for 60 days.
- **Dismissal**: free during probation. After that you need two active written
  warnings; otherwise it's unfair dismissal: 80 hours' wages in compensation
  and everyone's morale drops.

### Careers, reviews and promotions
- Ladder: **Staff > Shift Lead > Department Manager > Assistant Manager**.
  - Shift Lead (store level 2, 10 days in, no active written warning, +$2.50):
    staff around them slack less and work a little faster.
  - Department Manager (level 5, 20 days in, +$2.50): runs one department:
    supervises its stockers and orders its stock before their shift ends.
  - Assistant Manager (level 3, one only, +$3): the delegation jobs below.
  - Demoting is possible, but people take it badly.
- **Performance reviews** every 30 days: a 1-5 rating from the time clock,
  output and conduct. Good ratings expect a raise; a missed or overdue review
  costs morale.
- **Ambitious** staff who aren't promoted after about 25 days get restless and
  may leave for a job with more responsibility.

### Morale
- Morale (0-100) moves with pay vs asking wage, workload, praise, warnings,
  rest between shifts, days off, hours vs contract, overtime, reviews and
  promotions. Each employee shows why it moved last shift. Low morale means
  slower work, more slacking and sick days, and eventually quitting.

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
- **Look:** every shopper is put together from body parts (`Characters.fbx`):
  teens, adults and older people (about 15%, slower, some with a cane), all
  builds and heights, many hairstyles, beards and glasses, tees, hoodies,
  sweaters, jackets, coats and skirts. A list of 7+ items means a **trolley**,
  otherwise a **basket**; what they pick shows up in it. After paying, basket
  shoppers leave with a paper bag and trolley shoppers with bags in the trolley.

### People (models and animation)

- Parts are named `C_<Part>__<colour role>`; the game tints skin, hair, top,
  bottom, shoes and accents per person and keeps the palette for details.
- `CharacterRig` assembles hips, torso, head, arms and legs and animates them
  in code: walking (legs, arm swing, bob), carrying a basket, pushing a
  trolley, and poses — stocking/cleaning, scanning at the till, reaching for a
  shelf, on the phone (slacking), sitting at the desk.
- **Uniforms:** store polo in the role colour with logo and name badge.
  Cashier blue; stocker grey polo with an orange hi-vis vest; cleaner teal;
  baker cream polo, apron and baker's hat; deli white polo, striped maroon
  apron and cap; assistant manager white shirt, purple tie and lanyard. You
  wear a charcoal blazer, white shirt and red tie. Staff keep their look (it
  comes from their name) and change uniform when their role changes.
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
- Start with $20,000. Rent $350/day from the day you open. A starter shop (2 checkouts, 6 shelves) plus express opening stock
  costs about $9k. In the headless auto-player run, sales grow from ~$3k a day
  to ~$13k a day by day 30, with level 12 around day 30–35 and the big
  departments (bakery, deli) bought as money allows.

## Time

- 1 second at 1x = 30 game seconds. Open 08:00–22:00.
- Speeds: pause, 1x, 2x, 4x, 8x. While the store is closed, time runs 4x
  faster (can be turned off).
- Saving: see "Saving and loading" below.


## Saving and loading

- **Saves app** on the office computer: a quick-save slot (also **F5**, load with **F9**), three manual
  slots, and an **autosave every night at midnight**. Also **New game** (asks first).
- A save keeps everything that matters: clock, money and the last 60 days of books, licences and level, the
  building and every fixture with its product and stock (with sell-by dates), the back room, orders,
  suppliers and bills, the order cart, all staff (contracts, rosters, records, warnings, what you know about
  them), applicants and interviews, and the journal.
- Customers in the store and spills don't carry over. Staff who were at work when you saved carry on with
  their shift. Loaded games start paused.
- Files are plain text in `Application.persistentDataPath/Saves`. Loading or a new game swaps in a fresh
  scene, so nothing from the old store is left behind.
- Test: `Tools/Headless/run.sh 30 scripted 11 savetest` saves mid-afternoon, loads into a fresh game, saves
  again and checks both saves match record for record, plays on two days, then starts a new game.

## Roadmap

1. **First slice (this build):** build mode with free placement, one product
   per shelf, licences and store levels, staff roles and shifts, assistant
   manager, customers, orders, express delivery, expansion, office/floor.
2. Staff: a proper break room, holidays and leave requests, staff swapping
   shifts, notice periods.
3. Suppliers with different prices and lead times; promotions and sales.
4. Decor and signs you can place; drag-to-place rows of shelves.
5. Self-checkouts, security/shoplifting, events (holidays, heat waves).
6. Real UI (UI Toolkit) replacing the IMGUI debug HUD.
7. Art pass: lighting, more animation (skinned rigs), facial expressions.
8. Save/load, difficulty and failure settings, more store types.
