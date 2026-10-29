# VNews — visual acceptance contract

## Goal
Portfolio news portal with visual density and editorial layout **close to the supplied VnExpress screenshot**, but with original VNews brand, permitted assets, and actual .NET/C#/SQL Server functionality.

## Fixed reference
- Desktop image: `references/vnexpress-desktop.png` (ideally Playwright screenshot viewport **1440x900** without browser chrome)
- Mobile image: `references/vnexpress-mobile.png` (viewport **390x844**, if reference is available)
- Local screenshots: `artifacts/visual/vnews-desktop.png`, `artifacts/visual/vnews-mobile.png`
- Reference should be captured **once** and treated as frozen. The news site changes over time; don't silently recapture every run.

## Measurable structure (confirm measurements by inspection)
- Center editorial container approximately 1100–1200 CSS px on desktop.
- Compact masthead including brand, date and utilities; full-width horizontal categories beneath.
- The primary top story region has a prominent **image on the left** and headline/summary **next to the image on the right**. The current VNews layout wrongly has text underneath a giant image.
- Smaller stories arranged under the first story, with consistent 16:9 cropped visuals and a compact hierarchy; secondary sidebar visually subordinate.
- Light borders, well-sized editorial type, restrained accent colors, even gutters and dense but readable content.
- On mobile, convert to an intentional single column; no clipped nav or horizontal scrolling.
- Do not reproduce the source site's ads, login system, brand logo or copyrighted photos/text. Use original permitted VNews demo imagery.

## Acceptance
- UI screenshot inspected at desktop 1440x900 and mobile 390x844.
- Main hero geometry, visual density, typography, photos and spacing close to reference; no giant SVG blocks.
- Static assets 200 with correct type; navigation, article and search work.
- All article lists remain bound to SQL Server through existing .NET services.
- `dotnet build VNews.slnx` succeeds, `dotnet test VNews.slnx` records actual results.
- No promise of pixel-identical output or a real VnExpress backend; report remaining discrepancies.
