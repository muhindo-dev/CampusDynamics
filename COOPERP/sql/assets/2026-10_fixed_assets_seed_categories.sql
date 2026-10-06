-- ---------------------------------------------------------------------------
-- Fixed Assets module: starting categories (database campus_dynamics)
-- File: COOPERP/sql/assets/2026-10_fixed_assets_seed_categories.sql
-- RUN ONLY AFTER MIS AND THE BURSAR APPROVE THE LIST IN THE PLAN (section 3).
-- Idempotent: keyed on (parent_id, code). Inserts only; never updates.
-- ---------------------------------------------------------------------------

-- Categories (parent_id = 0)
INSERT IGNORE INTO fa_category
 (parent_id, code, name, description, asset_type, dep_method, useful_life_years, dep_rate_pct, residual_pct,
  revalue_every_months, cap_threshold, gl_cost_account, gl_accum_account, gl_expense_account, sort_order, created_by, created_at)
VALUES
 (0,'LB','Land and Buildings','Land, buildings and site works','TANGIBLE','SL',50,2.0000,0,12,NULL,'AC8022','AC8023','AC2161',10,'seed',NOW()),
 (0,'FF','Furniture and Fittings','Office, teaching and residential furniture, fixtures and fittings','TANGIBLE','SL',8,12.5000,0,NULL,NULL,'AC8021','AC8030','AC2161',20,'seed',NOW()),
 (0,'CE','Computer Equipment','Computers, servers, printers and peripherals','TANGIBLE','SL',3,33.3333,0,NULL,NULL,'AC8024','AC8026','AC2161',30,'seed',NOW()),
 (0,'OE','Office Equipment','Projectors, copiers, appliances and other office equipment','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8016','AC8041','AC2161',40,'seed',NOW()),
 (0,'LW','Laboratory and Workshop Equipment','Teaching laboratories, workshops and studios','TANGIBLE','SL',3,33.3333,0,NULL,NULL,'AC8025','AC8027','AC2161',50,'seed',NOW()),
 (0,'PM','Plant and Machinery','Generators, power and water systems, machines','TANGIBLE','SL',10,10.0000,0,NULL,NULL,'AC8016','AC8041','AC2161',60,'seed',NOW()),
 (0,'MV','Motor Vehicles','Buses, cars, pickups and motorcycles','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8035','AC8036','AC2161',70,'seed',NOW()),
 (0,'LM','Library Books and Materials','Printed and electronic library collections','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8020','AC8029','AC2161',80,'seed',NOW()),
 (0,'NC','Network and Communication Equipment','Data network, telephony, radio and CCTV','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8024','AC8026','AC2161',90,'seed',NOW()),
 (0,'SE','Sports Equipment','Field, gym and indoor sports equipment','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8016','AC8041','AC2161',100,'seed',NOW()),
 (0,'SW','Software and Licences','Institutional systems and software licences','INTANGIBLE','SL',4,25.0000,0,NULL,NULL,'AC8037','AC8038','AC2163',110,'seed',NOW());

-- Sub-categories. NULL defaults inherit from the parent.
INSERT IGNORE INTO fa_category
 (parent_id, code, name, description, asset_type, dep_method, useful_life_years, dep_rate_pct, residual_pct,
  revalue_every_months, cap_threshold, gl_cost_account, gl_accum_account, gl_expense_account, sort_order, created_by, created_at)
SELECT p.id, s.code, s.name, s.description, s.asset_type, s.dep_method, s.life, s.rate, NULL, NULL, NULL,
       s.gl_cost, s.gl_accum, s.gl_exp, s.sort_order, 'seed', NOW()
FROM fa_category p
JOIN (
  SELECT 'LB' pc,'FHL' code,'Freehold land' name,'Land held on freehold or mailo title; not depreciated' description,NULL asset_type,'NONE' dep_method,NULL life,NULL rate,'AC8039' gl_cost,NULL gl_accum,NULL gl_exp,10 sort_order
  UNION ALL SELECT 'LB','LHL','Leasehold land','Amortised over the lease term (Masaka 99 years, Mubende 49 years)',NULL,'SL',49,NULL,'AC8031','AC8032','AC2162',20
  UNION ALL SELECT 'LB','BLD','Buildings','Teaching, administrative and residential buildings',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'LB','EXT','External works and site improvements','Fences, roads, drainage, external lighting',NULL,'SL',20,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'FF','OFF','Office furniture','Desks, chairs, cabinets and cupboards in offices',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'FF','TCH','Teaching furniture','Lecture desks, chairs, whiteboards, examination furniture',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'FF','RES','Residential furniture','Hostel and staff housing furniture',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'FF','FIT','Fixtures and fittings','Curtains, signboards, partitions, door labels',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'CE','DSK','Desktop computers','Desktop computers including laboratory machines',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'CE','LAP','Laptops and tablets','Portable computers',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'CE','SRV','Servers and storage','Servers, storage and data centre equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'CE','PRN','Printers and scanners','Printers, scanners and multifunction printers',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'CE','PER','Peripherals and UPS units','Monitors, UPS units and other peripherals',NULL,NULL,NULL,NULL,NULL,NULL,NULL,50
  UNION ALL SELECT 'OE','PRJ','Projectors and screens','Multimedia projectors and display screens',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'OE','CPY','Photocopiers','Photocopying machines',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'OE','APL','Office appliances','Air conditioners, refrigerators, water dispensers',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'OE','SEC','Safes and security equipment','Safes, strong boxes, metal detectors',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'OE','OTH','Other office equipment','Office equipment not listed elsewhere',NULL,NULL,NULL,NULL,NULL,NULL,NULL,50
  UNION ALL SELECT 'LW','SCI','Science laboratory equipment','Science laboratory apparatus and instruments',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'LW','ENG','Engineering workshop equipment','Civil, electrical and mechanical engineering equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'LW','MED','Media and studio equipment','Cameras, sound and studio equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'LW','HTL','Hospitality training equipment','Kitchen and hotel training equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'LW','ART','Art and design equipment','Studio and design equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,50
  UNION ALL SELECT 'PM','GEN','Generators','Standby generators',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'PM','SOL','Solar and power systems','Solar installations, inverters, power systems',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'PM','WTR','Water tanks and pumps','Water storage tanks and pumps',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'PM','MCH','Machines and tools','Engraving and other machines and power tools',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'MV','BUS','Buses and coasters','Passenger buses and coasters',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'MV','CAR','Cars and pickups','Saloon cars, station wagons and pickups',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'MV','MCY','Motorcycles','Motorcycles',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'LM','BKS','Printed books','Book collections recorded by acquisition lot',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'LM','JNL','Journals and serials','Bound journals and serials',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'LM','ELB','E-library and digital resources','Perpetual electronic collections','INTANGIBLE',NULL,NULL,NULL,'AC8028','AC8040',NULL,30
  UNION ALL SELECT 'NC','NET','Switches, routers and access points','Active data network equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'NC','CAB','Structured cabling','Network cabling and cabinets',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'NC','TEL','Telephony and radio','Telephone systems, handsets and radios',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'NC','CCT','CCTV and access control','CCTV cameras, recorders and access control',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'SE','FLD','Field and outdoor equipment','Goal posts, nets and outdoor equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'SE','GYM','Gym and fitness equipment','Gym machines and fitness equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'SE','IND','Indoor games equipment','Tables and equipment for indoor games',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'SW','ERP','Institutional systems','ERP, finance and library systems',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'SW','LIC','Software licences','Perpetual or multi-year software licences',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
) s ON s.pc = p.code AND p.parent_id = 0;
