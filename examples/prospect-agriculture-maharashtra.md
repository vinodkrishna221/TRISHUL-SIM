# Worked Example — /prospect run · AGRICULTURE × MAHARASHTRA · August 2026

> A real run of the problem-prospector method with live web mining. Study the SHAPE:
> every problem below was **mined** from institutional evidence (audits, assembly replies,
> court orders, procurement docs, grievance dashboards, shutdown postmortems) — not
> brainstormed. Every claim carries a URL; items not traceable to a primary document are
> labelled [UNVERIFIED]. Figures quoted as stated in the cited source (verify freshness
> before submitting — evidence rots).

## A · Pipeline log (highlights)

**P1 ADMISSION:** CAG: Maharashtra micro-irrigation SC/ST funds — ₹187.46 cr released,
only ₹39.78 cr used ([National Herald](https://www.nationalheraldindia.com/india/maharashtra-farmers-suffer-as-state-did-not-utilise-funds-says-cag)) ·
Assembly reply Mar-2025: **4,30,443 PMFBY applications rejected Kharif 2024** vs 2.85L in 2023, Beed fraud hotspot ([Business Standard](https://www.business-standard.com/politics/over-403k-crop-insurance-claims-rejected-in-maharashtra-manikrao-kokate-125031000561_1.html)) ·
Assembly reply 2023: 14.28 lakh ineligible PM-KISAN beneficiaries got ₹1,754.5 cr, only ₹93.21 cr recovered ([BusinessLine](https://www.thehindubusinessline.com/economy/agri-business/pm-kisan-1428-lakh-ineligible-beneficiaries-in-maharashtra-receive-1-754-crore/article67105357.ece))

**P2 GRIEVANCE:** CPGRAMS Dec-2024: **88.32% of all agri-dept grievances = "PM-KISAN instalment stopped"** ([PIB PDF](https://static.pib.gov.in/WriteReadData/specificdocs/documents/2025/jan/doc2025131492301.pdf)) ·
Maharashtra = **#2 state for grievance pendency** (20,957 pending) ([PIB PDF](https://static.pib.gov.in/WriteReadData/specificdocs/documents/2024/dec/doc20241218474001.pdf)) ·
PMFBY 2023-24: ₹10,141 cr premium in, ₹3,551 cr paid out ([Frontline](https://frontline.thehindu.com/news/farmers-government-scheme-maharashtra-crop-insurance-kisan-sabha-india/article68468781.ece)) ·
"₹6 compensation after 2-month wait" outrage ([NDTV](https://www.ndtv.com/india-news/maharashtra-farmer-waits-months-for-crop-loss-compensation-gets-rs-6-9581501))

**P3 PROCUREMENT (buying = admitting):** MahaAgri-AI Policy 2025-29 — **₹500 cr** to buy solutions for "persistent challenges" incl. grievance automation ([policy PDF](https://agritech.tnau.ac.in/pdf/Maha%20Agri-AI%20Policy%202025%E2%80%932029_English_250619_104818.pdf)) ·
AIAIC CfP: pilots up to ₹2 cr ([krishi.maharashtra.gov.in](http://krishi.maharashtra.gov.in/Site/Upload/Pdf/CFPBackground.pdf)) ·
Pune Agri Hackathon reposts "manual crop survey" + "fragmented scheme administration" statements 2025→2026 ([statements](https://www.puneagrihackathon.com/hackathon-statement))

**P4 GRAVEYARD:** BharatAgri shut Nov-2025 — TAM too small direct-to-farmer ([Inc42](https://inc42.com/buzz/agritech-startup-bharatagri-shuts-operations-due-to-funding-crunch/)) ·
AgroStar rural CAC ₹1,800 vs ₹2,200 annual txn value [UNVERIFIED — secondary] · Kheyti wound down despite Earthshot win [UNVERIFIED — secondary].
**Cross-cutting law: you cannot profitably acquire a smallholder directly — ride a rail that already holds the farmer.**

**P5 FRICTION:** Onion crash recurs **2023, 2024, 2025, 2026** — Lasalgaon −71% (₹3,900→₹1,100/qtl) ([TOI](https://timesofindia.indiatimes.com/city/nashik/impact-of-centres-ban-on-onion-export-41-dip-in-foreign-currency-earnings/articleshow/111046426.cms)); NAFED relief at ₹12.35/kg vs ₹18-20 cost ([Indian Express](https://indianexpress.com/article/cities/pune/maharashtra-onion-farmers-call-centres-procurement-price-inadequate-10694119/)) ·
Jalgaon: **₹60.8 cr undisbursed to 70,793 farmers because eKYC pending** ([Loksatta](https://www.loksatta.com/nashik/over-70000-jalgaon-farmers-still-waiting-for-crop-loss-compensation-after-heavy-rains-in-september-rds-00-5493781/)) ·
MSP centres opened 1 month late; farmers pre-sold soybean ₹3,200 vs MSP ₹5,238 ([FPJ](https://www.freepressjournal.in/pune/nanded-farmers-upset-as-msp-centres-open-one-month-late-most-forced-to-sell-produce-cheap)) ·
NGT ordered MPCB to CREATE a crop-damage recording SOP — court admitting no standard exists ([TOI](https://timesofindia.indiatimes.com/city/pune/ngt-directs-sugar-co-op-to-pay-rs1-06cr-for-polluting-ghod-river/articleshow/128762239.cms))

## B · Triangulated candidates (6 survived the gates)

C1 eKYC/land-record gate strands sanctioned relief · C2 onion crash + late/underpriced
relief (4-yr recurrence) · C3 PMFBY premium-in/payout-out gap + mass-rejection pendulum ·
C4 manual panchnama throttles compensation · C5 MSP centres open after distress-sales ·
C6 sugar-mill effluent proven only via years of NGT litigation.
(C3 died at solvability — the bleeding link isn't outsider-buildable in 36h; C5 thin
software surface; C6 narrow population.)

## C · Top 3 (full dossier treatment)

### 🥇 C1 — "Why is my paisa stuck?" — the eKYC/land-record gate
- **Severity:** ₹60.8 cr / 70,793 farmers stranded in ONE district; 5,00,000 facing
  loan-waiver exclusion; 2.31 lakh can't get Farmer IDs (7/12 mismatches); 88.32% of
  national agri grievances = "instalment stopped". **Recurrence:** 2023/2025/2026 — structural.
- **Failed attempt:** AgriStack Farmer Registry was meant to clean the pipe; it became
  the new choke point, failing silently at the land-record layer — eligible farmers get
  zero and never learn why. **Pain owner:** Tehsildar/Collector + Agriculture Dept.
- **Whitespace:** nobody owns the *diagnostic* layer ("which record do I fix, where").
- **WEDGE (36h):** WhatsApp/IVR bot — farmer sends Farmer-ID/survey no. → failure class
  (eKYC pending / bank not seeded / 7-12 name mismatch / mutation pending) → the ONE next
  action + exact office + pre-filled Aaple-Sarkar grievance. Official side: failure-reason
  heat-map per village → targeted eKYC camps. **Rail:** AgriStack/KYS + Aaple Sarkar.
- **Deploy Monday:** pilot with one Collectorate's existing undisbursed list — the pain
  owner literally hands you the users. Metric: % stranded → paid in one cycle.

### 🥈 C4 — Standardised geo-tagged panchnama
- **Severity:** ₹18.92 cr sanctioned yet stuck on assessment (Ahilyanagar); DyCM ordering
  panchnamas "without waiting"; NGT ordering an SOP be *created*. Every rain event, 3 years.
- **WEDGE:** field-capture app for Talathis — GPS+timestamp+photo+crop, area auto-pulled
  from 7/12 → tamper-evident standardised damage record flowing into the relief proposal.
  **Rail:** the Revenue Dept's own field staff (distribution = government workforce).
- **Procurement path already funded:** the state's own hackathon + AIAIC ₹2 cr pilots
  repost this exact need.

### 🥉 C2 — Onion distress-sale early-warning + procurement slotting
- **Severity:** −71% price crash; ₹10,000 cr claimed loss; relief structurally late +
  below cost. Strongest recurrence in the set (4 consecutive years).
- **Whitespace nuance:** price DISCOVERY is covered (Agmarknet/eNAM) — the open gap is
  relief TIMING/targeting. **WEDGE:** crash-threshold trigger on APMC feeds → WhatsApp/IVR
  alert to growers with nearest NAFED/NCCF slot + booking; agency heat-map of arrival
  spikes. Framed honestly as triage, not cure (the real fix is policy).

## Method takeaways (why this beats a ChatGPT problem list)

1. The winning problem (C1) is invisible to brainstorming — it lives in the gap BETWEEN
   schemes, visible only where grievance %, assembly replies, and district news triangulate.
2. The graveyard law shaped every wedge: ride a rail that already holds the user.
3. Procurement mining proved demand: the state has budgeted ₹500 cr trying to buy what
   these wedges do — a self-proposed PS with a named funded buyer is judge catnip.
4. Total run: ~4 hours of agent mining. Evidence rots — re-verify figures before submission.
