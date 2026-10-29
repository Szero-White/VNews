# Visual target — VNews, editorial Vietnamese portal

Reference: https://vnexpress.net/ for general information architecture, NOT a source for copied assets. If user provides a screenshot, put it at `references/homepage.png` and use it as visual reference. Independent brand VNews.

## Desktop (~1440px)
- Clean white background, generous margins, restrained dark typography, strong editorial hierarchy, muted crimson brand accent.
- Slim top utility strip (date/edition); prominent brand masthead with search and account actions.
- Horizontal category navigation below masthead; centered content max width ~1200px.
- Front page: large lead visual + bold headline left (~2/3 width), two smaller stories to right; below: card grids and chronological stories; optional side column of notable reads.
- Category page: lead story, supporting list, page navigation. Article page: metadata, large readable heading, summary, body in narrow column, related stories, moderated comments later.
- Image aspect ratios predictable; locally bundled safe placeholders allowed, but no broken images.

## Responsive (~768px and 390px)
- Replace crowded desktop nav with working mobile menu; no horizontal overflow; large enough taps.
- Hero becomes stacked; grid collapses to 1 column on narrow phones; long Vietnamese headings wrap naturally.
- Favor meaningful whitespace and readable 16px+ body; no generic dashboard styling on the public site.

## Feedback loop
1. Implement semantic HTML/CSS and real Razor page.
2. Start real app and screenshot at 1440px and 390px IF a browser/preview tool is available.
3. Compare composition, typography, gaps, image ratios, navigation with `references/homepage.png` if present. Fix the 3 biggest mismatches per iteration; stop after 2 purposeful visual passes unless user requests more.
4. Do not fabricate screenshots or say visually verified if you cannot inspect a running browser.
