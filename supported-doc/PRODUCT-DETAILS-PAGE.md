# Product Details Page (تفاصيل المنتج / PDP) — Build Spec for Claude Code

A precise, self-contained spec to rebuild the **Product Details / PDP** page exactly as it currently appears — same layout, structure, and Arabic text. RTL Arabic.

> Page direction: `dir="rtl" lang="ar"`. Font: **IBM Plex Sans Arabic**. Everything aligns right-to-left.
> Sits between the global chrome (top bar + navbar + category strip) and the footer. This doc covers the `<main>` content only.

---

## Page structure (top → bottom)

```
<main> (inside .wrap, 1240px centered)
1. Breadcrumbs (.crumbs)
2. Product hero (.pdp)            2-col: gallery (right) + info (left)
3. Details band (.detail2)        2 cards: product details + about the craft
4. Related products (.sec)        section head + 4 product cards (same category)
```

The product is resolved by id from `params.id` (falls back to the first product). On product change, quantity resets to 1, selected image resets to 0, and the page scrolls to top.

---

## 1. Breadcrumbs (`.crumbs`)

Small, muted, chevron separators (padding-top 22px):
`الرئيسية` › `المتجر` › `{category name}` › `{product name}`
- "الرئيسية" → home, "المتجر" → listing, category → listing preset to that category, product name = current (bold).

---

## 2. Product hero (`.pdp`)

**2-column grid:** `grid-template-columns: 1fr 1fr; gap: 50px; align-items: start;` padding-block 36px. Collapses to **1 column** under 980px.

### Right column — Gallery (`.pdp__gallery`, sticky)
Sticky at `top: nav-height + 16px`.
- **Main image** (`.pdp__main`): photo placeholder, aspect-ratio 1:1, radius 28px, shadow. Tinted by product `tone`. Label: `{name} — صورة {n}` where n = selected index + 1.
- **Thumbnails** (`.pdp__thumbs`): 4-column grid, gap 12px. Four thumbnails (same tone), radius 14px, clickable. The selected one has a green 2px border (`.is-on`). Clicking sets the active image.

### Left column — Info (`.pdp__info`)
In order:
- **Category eyebrow** (`.pdp__cat`, gold, 0.9rem): the category name.
- **H1** (`.pdp__title`, `clamp(1.7rem, 3.2vw, 2.4rem)`, margin-top 8px): product name.
- **Meta row** (`.pdp__meta`, flex, wrap, muted, dots between):
  - Rating: stars + numeric value (`p.rating.toFixed(1)`)
  - `{reviews} تقييمًا`
  - tag badge (gold) — only if the product has a `tag`
  - in-stock badge (green, check icon): `متوفر`
- **Price** (`.pdp__price`, margin-top 22px): large price (`Price` with `big`), and if discounted, a sale badge: `وفّر {old−price} د.ا`.
- **Description** (`.pdp__desc`, muted, line-height 1.8, margin-top 22px): the product `desc` followed by a fixed handmade disclaimer:
  `{desc} كل قطعة فريدة وقد تختلف اختلافًا طفيفًا عن الصورة لأنها مصنوعة يدويًا بالكامل.`
- **Origin card** (`.pdp__origin`): ivory-2 box, padding 16–18px, radius 14px, margin-top 24px. A navy round pin-icon chip (46px) + text:
  - bold: `صُنع في {center}`
  - sub (muted): `منتج يدوي موثّق المصدر — عائد البيع يدعم تأهيل صانعه`
- **Buy row** (`.pdp__buy`, flex, wrap, margin-top 24px):
  - **Quantity stepper** (`.qty`): bordered pill with `−` button, current value (`.num`), `+` button. Min 1, no max.
  - **Primary green large button** (flex-grows): `أضف إلى السلة` + cart icon → adds `qty` of the product to cart, then navigates to the cart page.
  - **Ghost large button** = wishlist toggle (heart icon, line/filled). When wished, it turns red (`color:#C0392B; borderColor:#E2B6AC`).
- **Perks grid** (`.pdp__perks`): 2-column grid, margin-top 28px, top border. Renders the 4 `TRUST` items — each = green icon chip (40px) + bold title + small muted subtext. Collapses to 1 column < 600px.

---

## 3. Details band (`.detail2`)

A `.sec` (padding-bottom 0) containing a **2-column grid** (`grid-template-columns: 1fr 1fr; gap: 20px`), collapses to 1 column < 760px. Two cards (`.formcard`, white, bordered, radius 20px, padding 26px):

**Card A — `تفاصيل المنتج`** (`<h3>` title). A spec list (`.speclist`) of label/value rows separated by bottom borders:
| Label | Value |
|---|---|
| `الفئة` | {category name} |
| `المركز المُنتِج` | {center} |
| `طريقة الصنع` | `يدوي بالكامل` |
| `المادة` | `خامات طبيعية` |
| `التوصيل` | `٣–٥ أيام عمل` |

**Card B — `عن الحرفة`** (`<h3>` title). A muted paragraph (line-height 1.8):
`ضمن برنامج «صُنع بعزيمة»، يتلقّى النزلاء تدريبًا متخصصًا على الحِرَف اليدوية داخل مراكز الإصلاح والتأهيل. المنتجات تخضع لمعايير جودة دقيقة قبل عرضها، وكل عملية شراء تُسهم مباشرةً في تمكينهم اقتصاديًا وإعادة دمجهم في المجتمع.`
Followed by a small ghost button (margin-top 16px): `تعرّف على البرنامج` + arrow → about page.

---

## 4. Related products (`.sec`)

Only shown if there are related items. **Section head** (`.sec-head`): H2 `منتجات مشابهة` + right link `عرض الكل` → listing preset to the product's category.

**Content:** `ProductGrid` of up to **4 products** in the **same category**, excluding the current product (`PRODUCTS.filter(x => x.cat === p.cat && x.id !== p.id).slice(0, 4)`).

---

## 5. Colors & tokens used

| Role | Hex |
|---|---|
| Page background | `#FBF5E9` (ivory) |
| Card / surface | `#FFFFFF` |
| Origin box | `#F4EBD9` (ivory-2) |
| Border | `#E6DAC2` (strong `#D8C9AB`) |
| Heading / title text | `#394142` |
| Muted text | `#5A6364` |
| Category eyebrow / gold | `#A7790F` |
| Primary action (add to cart) | green `#5C9A3D` |
| Origin pin chip / thumbnails active | navy `#2E4F72` / green border |
| In-stock badge | green-soft bg + green text |
| Sale badge | `#F6E2DC` bg / `#B0492C` text |
| Wishlist active | `#C0392B` |
| Perk icon chips | green-soft `#EAF1E2` / green strong `#4C8330` |

- Font: `"IBM Plex Sans Arabic", system-ui, sans-serif`. Prices: `د.ا`, **3 decimals**, Latin tabular figures (`.num`); Arabic-Indic numerals in labels.
- Radii: main image 28px, cards 20px, thumbnails/qty/buttons 14px, badges/pills round.
- Max width 1240px; side gutter `clamp(16px, 4vw, 40px)`.

---

## 6. Responsive behavior

| Breakpoint | Change |
|---|---|
| ≥ 980px | 2-col hero (sticky gallery), perks 2-col |
| < 980px | Hero → 1 column; gallery no longer sticky |
| < 760px | Details band → 1 column |
| < 600px | Perks grid → 1 column |

---

## 7. State & interactions

- **`qty`** (default 1): controlled by the stepper; floor of 1.
- **`img`** (default 0): selected gallery image; thumbnails set it.
- **Add to cart:** `addToCart(p.id, qty)` then `go("cart")`.
- **Wishlist:** `toggleWish(p.id)`; `wished = wishlist.includes(p.id)` drives heart fill + red styling.
- **On `p.id` change:** reset qty/img, scroll to top (`useEffect`).
- **Props in:** `go`, `params` (`{id}`), and cart API (`addToCart`, `toggleWish`, `wishlist`, plus the rest spread onto related-product cards).

---

## 8. Reference: current React implementation

Component: `ProductScreen` in `product.jsx`. Dependencies: `Photo`, `Icon`, `Stars`, `Price`, `fmt` (`ui.jsx`); `ProductGrid` (`cards.jsx`); `CATEGORIES`, `CENTERS`, `PRODUCTS`, `TRUST` (`data.jsx`); CSS classes `.pdp*`, `.qty`, `.detail2`, `.speclist`, `.formcard`, `.sec-head` (`screens.css`).

```jsx
function ProductScreen({ go, params, ...cart }){
  const p = PRODUCTS.find(x=>x.id===params.id) || PRODUCTS[0];
  const [qty, setQty] = useState(1);
  const [img, setImg] = useState(0);
  const cat = CATEGORIES.find(c=>c.id===p.cat);
  const related = PRODUCTS.filter(x=>x.cat===p.cat && x.id!==p.id).slice(0,4);
  const wished = cart.wishlist.includes(p.id);

  useEffect(()=>{ setQty(1); setImg(0); window.scrollTo(0,0); }, [p.id]);

  return (
    <main>
      <div className="wrap">
        <nav className="crumbs" style={{paddingTop:22}}>
          <a href="#" onClick={(e)=>{e.preventDefault();go("home");}}>الرئيسية</a>
          <Icon name="chevron" size={14} />
          <a href="#" onClick={(e)=>{e.preventDefault();go("listing");}}>المتجر</a>
          <Icon name="chevron" size={14} />
          <a href="#" onClick={(e)=>{e.preventDefault();go("listing",{cat:p.cat});}}>{cat?.name}</a>
          <Icon name="chevron" size={14} />
          <span>{p.name}</span>
        </nav>

        <div className="pdp">
          <div className="pdp__gallery">
            <Photo tone={p.tone} label={p.name + " — صورة " + (img+1)} className="pdp__main" />
            <div className="pdp__thumbs">
              {[0,1,2,3].map(i=>(
                <div key={i} className={"pdp__thumb"+(img===i?" is-on":"")} onClick={()=>setImg(i)}>
                  <Photo tone={p.tone} label="" />
                </div>
              ))}
            </div>
          </div>

          <div className="pdp__info">
            <span className="pdp__cat">{cat?.name}</span>
            <h1 className="pdp__title">{p.name}</h1>
            <div className="pdp__meta">
              <span className="pcard__rating"><Stars value={p.rating} /> <span className="num">{p.rating.toFixed(1)}</span></span>
              <span className="dot"></span>
              <span>{p.reviews} تقييمًا</span>
              <span className="dot"></span>
              {p.tag && <span className="badge badge--gold">{p.tag}</span>}
              <span className="badge badge--green"><Icon name="check" size={13} /> متوفر</span>
            </div>

            <div className="pdp__price">
              <Price now={p.price} old={p.old} big />
              {p.old && <span className="badge badge--sale">وفّر {fmt(p.old-p.price)} د.ا</span>}
            </div>

            <p className="pdp__desc">{p.desc} كل قطعة فريدة وقد تختلف اختلافًا طفيفًا عن الصورة لأنها مصنوعة يدويًا بالكامل.</p>

            <div className="pdp__origin">
              <span className="ic"><Icon name="pin" size={22} /></span>
              <span>
                <b>صُنع في {CENTERS[p.center]}</b>
                <span>منتج يدوي موثّق المصدر — عائد البيع يدعم تأهيل صانعه</span>
              </span>
            </div>

            <div className="pdp__buy">
              <div className="qty">
                <button onClick={()=>setQty(q=>Math.max(1,q-1))} aria-label="إنقاص"><Icon name="minus" size={18} /></button>
                <span className="num">{qty}</span>
                <button onClick={()=>setQty(q=>q+1)} aria-label="زيادة"><Icon name="plus" size={18} /></button>
              </div>
              <button className="btn btn--primary btn--lg" onClick={()=>{cart.addToCart(p.id,qty);go("cart");}}>
                <Icon name="cart" size={19} /> أضف إلى السلة
              </button>
              <button className="btn btn--ghost btn--lg" onClick={()=>cart.toggleWish(p.id)} aria-label="المفضلة"
                style={wished?{color:"#C0392B",borderColor:"#E2B6AC"}:null}>
                <Icon name={wished?"heart":"heartLine"} size={19} />
              </button>
            </div>

            <div className="pdp__perks">
              {TRUST.map(t=>(
                <div className="pdp__perk" key={t.title}>
                  <span className="ic"><Icon name={t.icon} size={20} /></span>
                  <span><b>{t.title}</b><br/><small>{t.text}</small></span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* details band */}
        <section className="sec" style={{paddingBottom:0}}>
          <div className="detail2">
            <div className="formcard" style={{margin:0}}>
              <h3>تفاصيل المنتج</h3>
              <ul className="speclist">
                <li><span>الفئة</span><b>{cat?.name}</b></li>
                <li><span>المركز المُنتِج</span><b>{CENTERS[p.center]}</b></li>
                <li><span>طريقة الصنع</span><b>يدوي بالكامل</b></li>
                <li><span>المادة</span><b>خامات طبيعية</b></li>
                <li><span>التوصيل</span><b>٣–٥ أيام عمل</b></li>
              </ul>
            </div>
            <div className="formcard" style={{margin:0}}>
              <h3>عن الحرفة</h3>
              <p className="muted" style={{lineHeight:1.8}}>
                ضمن برنامج «صُنع بعزيمة»، يتلقّى النزلاء تدريبًا متخصصًا على الحِرَف اليدوية داخل مراكز الإصلاح والتأهيل.
                المنتجات تخضع لمعايير جودة دقيقة قبل عرضها، وكل عملية شراء تُسهم مباشرةً في تمكينهم اقتصاديًا وإعادة دمجهم في المجتمع.
              </p>
              <button className="btn btn--ghost btn--sm" style={{marginTop:16}} onClick={()=>go("about")}>
                تعرّف على البرنامج <Icon name="arrowL" size={16} />
              </button>
            </div>
          </div>
        </section>

        {/* related */}
        {related.length>0 && (
          <section className="sec">
            <div className="sec-head">
              <h2>منتجات مشابهة</h2>
              <a className="sec-link" href="#" onClick={(e)=>{e.preventDefault();go("listing",{cat:p.cat});}}>عرض الكل <Icon name="arrowL" /></a>
            </div>
            <ProductGrid items={related} go={go} {...cart} />
          </section>
        )}
      </div>
    </main>
  );
}
```

### Relevant CSS (key rules)
```css
.pdp{ display:grid; grid-template-columns:1fr 1fr; gap:50px; padding-block:36px; align-items:start; }
.pdp__gallery{ position:sticky; top:calc(var(--nav-h) + 16px); }
.pdp__main{ aspect-ratio:1/1; border-radius:28px; box-shadow:var(--sh-2); }
.pdp__thumbs{ display:grid; grid-template-columns:repeat(4,1fr); gap:12px; margin-top:14px; }
.pdp__thumb{ aspect-ratio:1/1; border-radius:14px; border:2px solid transparent; cursor:pointer; overflow:hidden; }
.pdp__thumb.is-on{ border-color:var(--green); }
.pdp__title{ font-size:clamp(1.7rem,3.2vw,2.4rem); margin-top:8px; }
.pdp__meta{ display:flex; align-items:center; gap:16px; margin-top:14px; flex-wrap:wrap; }
.pdp__meta .dot{ width:4px; height:4px; border-radius:50%; background:var(--line-strong); }
.pdp__origin{ display:flex; align-items:center; gap:14px; background:var(--ivory-2); border-radius:14px; padding:16px 18px; margin-top:24px; }
.qty{ display:inline-flex; align-items:center; border:1.5px solid var(--line-strong); border-radius:14px; overflow:hidden; }
.qty button{ width:46px; height:50px; display:flex; align-items:center; justify-content:center; }
.pdp__buy{ display:flex; gap:14px; margin-top:24px; flex-wrap:wrap; }
.pdp__buy .btn--primary{ flex:1; min-width:200px; }
.pdp__perks{ display:grid; grid-template-columns:1fr 1fr; gap:14px; margin-top:28px; padding-top:24px; border-top:1px solid var(--line); }
.detail2{ display:grid; grid-template-columns:1fr 1fr; gap:20px; }
.speclist li{ display:flex; justify-content:space-between; gap:16px; padding:11px 0; border-bottom:1px solid var(--line); }
@media (max-width:980px){ .pdp{ grid-template-columns:1fr; gap:30px; } .pdp__gallery{ position:static; } }
@media (max-width:760px){ .detail2{ grid-template-columns:1fr; } }
@media (max-width:600px){ .pdp__perks{ grid-template-columns:1fr; } }
```

### Product data shape (from `data.jsx`)
```js
PRODUCT = { id, name, cat, tone, price, old?, rating, reviews, center, tag?, desc }
// cat → CATEGORIES id ; center → index into CENTERS
// tone tints the photo placeholder: clay/sand/sage/slate/gold/rose/olive/titanium/navy
```

---

## 9. Build checklist
- [ ] RTL layout; IBM Plex Sans Arabic; sits between navbar and footer.
- [ ] Breadcrumbs: home › store › category › product.
- [ ] 2-col hero: sticky gallery (1:1 main + 4 thumbnails with green active border) | info column.
- [ ] Info: category eyebrow, H1, meta row (rating + reviews + tag + in-stock), big price + savings badge, description + handmade disclaimer, origin card (center + mission line).
- [ ] Buy row: quantity stepper (min 1), green "أضف إلى السلة" → adds qty then opens cart, wishlist heart toggle (red when active).
- [ ] Perks: 4 trust items in 2 cols.
- [ ] Details band: two cards — spec list + "عن الحرفة" paragraph with link to about.
- [ ] Related products: up to 4 from same category, excluding current.
- [ ] All Arabic text verbatim; prices `د.ا` 3 decimals; reset qty/img + scroll-top on product change.
- [ ] Responsive collapses per the table.
```
```
