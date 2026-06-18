# Cart & Checkout Page (السلة / الدفع) — Build Spec for Claude Code

A precise, self-contained spec to rebuild the **Cart → Checkout → Confirmation** flow exactly as it currently appears — same layout, structure, steps, and Arabic text. RTL Arabic.

> Page direction: `dir="rtl" lang="ar"`. Font: **IBM Plex Sans Arabic**. Everything aligns right-to-left.
> Sits between the global chrome (top bar + navbar + category strip) and the footer. One component drives **three stages** via internal state.

---

## Overview — one screen, three stages

The page is a single component (`CartScreen`) with a `stage` state machine:

```
stage = "cart"  →  "checkout"  →  "done"
```

A **steps indicator** (`.steps`) sits at the top of every stage: `السلة` → `الدفع` → `التأكيد`.

There is also an **empty-cart** state shown (instead of cart/checkout) whenever the cart has no items.

**Layout for cart & checkout stages** (`.cartwrap`): 2-column grid `grid-template-columns: 1fr 380px; gap: 34px; align-items: start;`. Left = items/forms, right = **sticky order summary** (`top: nav-height + 16px`). Collapses to 1 column under 980px (summary no longer sticky).

On every `stage` change → scroll to top.

---

## Steps indicator (`.steps`)

A horizontal row of 3 steps separated by thin bars (`.bar`). Each step (`.step`) = a numbered circle (`.n`) + label. States:
- **is-on** (current): navy filled circle.
- **is-done** (completed): green filled circle with a check icon.
- default (upcoming): outlined, muted.

| Step | Number | Label |
|---|---|---|
| 1 | `١` (or ✓ once past) | `السلة` |
| 2 | `٢` (or ✓ when done) | `الدفع` |
| 3 | `٣` | `التأكيد` |

Numbers use Arabic-Indic numerals. On phones (<600px) the text labels hide, leaving the numbered circles.

---

## Empty cart state (`.emptycart`)

Shown when there are no line items (and not on the confirmation stage). Centered, padding ~70px:
- Round ivory icon chip (90px) with a cart icon.
- `<h2>`: `سلّتك فارغة`
- muted line: `أضف بعض القطع اليدوية الأصيلة لتبدأ.`
- Primary green large button: `تصفّح المتجر` + arrow → listing.

---

## Stage 1 — Cart (`stage === "cart"`)

**Left column** — a `.formcard` (white, bordered, radius 20px) listing the line items. Each item (`.citem`, grid `110px 1fr auto`, separated by bottom borders):
- **Media:** photo placeholder 110×110, tinted by product `tone`.
- **Body:**
  - Title (`<h3>`, clickable → product page).
  - Center line with pin icon: `{center}`.
  - Remove button (`.citem__rm`, muted, trash icon, turns red on hover): `إزالة` → removes the line.
- **End column** (`.citem__end`):
  - Line price = `Price(now = price × quantity)`.
  - Quantity stepper (`.qty`): `−` / value / `+`. Decreasing to 0 removes the item.

Below the list: a `.link-btn` (navy) with an arrow: `متابعة التسوّق` → listing.

**Right column** — order summary (see below) with the **"متابعة الدفع"** button.

---

## Stage 2 — Checkout (`stage === "checkout"`)

**Left column** — three stacked form cards (`.formcard`), each with an `<h3>` that has a small ivory icon chip:

**Card 1 — `معلومات التواصل`** (user icon):
- Row of 2 fields: `الاسم الكامل` (placeholder `الاسم الكامل`), `رقم الهاتف` (placeholder `٠٧ ____ ____`).
- Full-width field: `البريد الإلكتروني` (type email, placeholder `name@example.com`).

**Card 2 — `عنوان التوصيل`** (truck icon):
- Row of 2 fields: `المحافظة` (select: `عمّان`, `إربد`, `الزرقاء`, `العقبة`, `الكرك`, `معان`), `المنطقة` (placeholder `المنطقة / الحي`).
- Full-width field: `العنوان التفصيلي` (placeholder `الشارع، رقم المبنى، الطابق`).
- Full-width textarea: `ملاحظات (اختياري)` (placeholder `أي تفاصيل تساعد المندوب`).

**Card 3 — `طريقة الدفع`** (lock icon): three selectable payment options (`.payopt`, radio; selected one has green border + green-soft background):
| Value | Title | Sub | Trailing icon |
|---|---|---|---|
| `card` | `بطاقة ائتمان / مدى` | `Visa · Mastercard — دفع آمن ومشفّر` | lock |
| `cliq` | `CliQ / eFAWATEERcom` | `تحويل فوري عبر تطبيق بنكك` | phone |
| `cod` | `الدفع عند الاستلام` | `ادفع نقدًا عند وصول الطلب` | truck |

Default selected = `card`. Below options: a `.link-btn` with arrow: `العودة إلى السلة` → back to cart stage.

**Right column** — order summary with the **"تأكيد الطلب والدفع"** button (lock icon).

---

## Order summary (`.summary`) — shared by cart & checkout

White card, bordered, radius 20px, padding 24px, sticky. Contents:
- `<h3>`: `ملخّص الطلب`
- Row: `المجموع الفرعي ({count} قطعة)` … subtotal `{n} د.ا`
- Row: `الشحن` … `مجاني` (green) when free, else amount.
- **Promo (cart stage only)** (`.promo`): text input `رمز الخصم` + dark button `تطبيق`.
- **Total row** (`.sumrow.total`, larger/bold): `الإجمالي` … `Price(total)`.
- **Action button:**
  - Cart stage: green block large `متابعة الدفع` + arrow → sets stage to checkout.
  - Checkout stage: green block large `تأكيد الطلب والدفع` (lock icon) → sets stage to done.
- **Mission note** (`.note`, green-soft, hands icon): `١٠٠٪ من العائد يُوجَّه لدعم تأهيل النزلاء وإعادة دمجهم.`
- **Free-shipping nudge** (when shipping > 0): muted centered `أضف بقيمة {50−subtotal} د.ا للحصول على شحن مجاني`.

### Totals logic
```js
const lines    = cartItems.map(ci => ({ ...PRODUCTS.find(p=>p.id===ci.id), q:ci.q })).filter(x=>x.id);
const subtotal = lines.reduce((s,l)=> s + l.price*l.q, 0);
const shipping = (subtotal > 50 || subtotal === 0) ? 0 : 3;   // free over 50 د.ا
const total    = subtotal + shipping;
const count    = lines.reduce((s,l)=> s + l.q, 0);
```

---

## Stage 3 — Confirmation (`stage === "done"`)

Full-width centered block (`.confirm`, max 560px). The steps indicator still shows on top (all complete). Contents:
- Green round check chip (96px, green shadow).
- `<h1>`: `تم تأكيد طلبك!`
- muted line: `شكرًا لك — مساهمتك تدعم تأهيل صنّاع هذه القطع وإعادة دمجهم في المجتمع.`
- **Order recap card** (`.confirm__card`, white, bordered, text-aligned start):
  | Label | Value |
  |---|---|
  | `رقم الطلب` | `#SB-2026-0418` |
  | `التوصيل المتوقّع` | `٣–٥ أيام عمل` |
  | `طريقة الدفع` | `بطاقة ائتمان` / `CliQ` / `الدفع عند الاستلام` (from chosen `pay`) |
  | `الإجمالي المدفوع` (total row) | `Price(total)` |
- Two buttons (centered): primary `العودة للرئيسية` → clears cart + go home; ghost `متابعة التسوّق` → clears cart + go listing.

---

## Colors & tokens used

| Role | Hex |
|---|---|
| Page background | `#FBF5E9` (ivory) |
| Cards / summary | `#FFFFFF` |
| Border | `#E6DAC2` |
| Heading text | `#394142` |
| Muted text | `#5A6364` |
| Primary action / done step / free shipping | green `#5C9A3D` (`#4C8330`) |
| Current step circle | navy `#2E4F72` |
| Selected payment option | green border + green-soft `#EAF1E2` bg |
| Mission note | green-soft bg / green-strong text |
| Promo apply / dark button | navy `#2E4F72` |
| Remove (hover) | `#B0492C` |

- Font: `"IBM Plex Sans Arabic", system-ui, sans-serif`. Prices: `د.ا`, **3 decimals**, `.num` tabular figures; Arabic-Indic numerals in labels (step numbers, order #).
- Radii: cards/summary 20px, fields/buttons 14px (sm 10px), step circles + check chip round.
- Form inputs (`.field`): 1.5px border, radius 10px, navy border on focus.

---

## Responsive behavior

| Breakpoint | Change |
|---|---|
| ≥ 980px | 2-col (items/forms + sticky summary) |
| < 980px | 1 column; summary not sticky |
| < 600px | Line items collapse (media + body row, end row spans full width); address 2-field rows → 1 col; step labels hide (numbers remain) |

---

## State & interactions

- **`stage`**: `"cart" | "checkout" | "done"` (local). Scrolls to top on change.
- **`pay`**: `"card" | "cliq" | "cod"` (default `card`).
- **`promo`**: text input value (apply is a no-op placeholder).
- **Cart actions (passed in via `cart` prop):** `setQty(id, q)` (0 removes), `removeFromCart(id)`, `clearCart()`, plus `addToCart`. Cart items persist in `localStorage` (handled by the app shell).
- **Navigation:** `go("listing")`, `go("product",{id})`, `go("home")`.
- **Props:** `{ go, cartItems, cart }` where `cartItems = [{id, q}]`.

---

## Reference: current React implementation

Component: `CartScreen` in `cart.jsx`. Dependencies: `Photo`, `Icon`, `Price`, `fmt` (`ui.jsx`); `PRODUCTS`, `CENTERS` (`data.jsx`); CSS classes `.cartwrap`, `.steps/.step`, `.citem*`, `.summary/.sumrow/.promo/.note`, `.formcard`, `.field*`, `.payopt`, `.emptycart`, `.confirm*` (`screens.css`).

```jsx
function CartScreen({ go, cartItems, cart }){
  const [stage, setStage] = useState("cart"); // cart | checkout | done
  const [pay, setPay]     = useState("card");
  const [promo, setPromo] = useState("");

  const lines    = cartItems.map(ci=>({ ...PRODUCTS.find(p=>p.id===ci.id), q:ci.q })).filter(x=>x.id);
  const subtotal = lines.reduce((s,l)=>s + l.price*l.q, 0);
  const shipping = subtotal>50 || subtotal===0 ? 0 : 3;
  const total    = subtotal + shipping;
  const count    = lines.reduce((s,l)=>s+l.q,0);

  useEffect(()=>{ window.scrollTo(0,0); }, [stage]);

  const Steps = (
    <div className="steps">
      <div className={"step "+(stage==="cart"?"is-on":"is-done")}><span className="n">{stage==="cart"?"١":<Icon name="check" size={15}/>}</span><span>السلة</span></div>
      <div className="bar"></div>
      <div className={"step "+(stage==="checkout"?"is-on":stage==="done"?"is-done":"")}><span className="n">{stage==="done"?<Icon name="check" size={15}/>:"٢"}</span><span>الدفع</span></div>
      <div className="bar"></div>
      <div className={"step "+(stage==="done"?"is-on":"")}><span className="n">٣</span><span>التأكيد</span></div>
    </div>
  );

  // --- Confirmation stage ---
  if(stage==="done"){
    return (
      <main className="wrap">
        {Steps}
        <div className="confirm">
          <span className="ic"><Icon name="check" size={48} /></span>
          <h1>تم تأكيد طلبك!</h1>
          <p>شكرًا لك — مساهمتك تدعم تأهيل صنّاع هذه القطع وإعادة دمجهم في المجتمع.</p>
          <div className="confirm__card">
            <div className="sumrow"><span>رقم الطلب</span><b className="num">#SB-2026-0418</b></div>
            <div className="sumrow"><span>التوصيل المتوقّع</span><b>٣–٥ أيام عمل</b></div>
            <div className="sumrow"><span>طريقة الدفع</span><b>{pay==="card"?"بطاقة ائتمان":pay==="cliq"?"CliQ":"الدفع عند الاستلام"}</b></div>
            <div className="sumrow total"><span>الإجمالي المدفوع</span><Price now={total} /></div>
          </div>
          <div style={{display:"flex",gap:12,justifyContent:"center",marginTop:28,flexWrap:"wrap"}}>
            <button className="btn btn--primary" onClick={()=>{cart.clearCart();go("home");}}>العودة للرئيسية</button>
            <button className="btn btn--ghost" onClick={()=>{cart.clearCart();go("listing");}}>متابعة التسوّق</button>
          </div>
        </div>
      </main>
    );
  }

  // --- Empty cart ---
  if(lines.length===0){
    return (
      <main className="wrap">
        <div className="emptycart">
          <span className="ic"><Icon name="cart" size={42} /></span>
          <h2>سلّتك فارغة</h2>
          <p className="muted" style={{marginTop:8}}>أضف بعض القطع اليدوية الأصيلة لتبدأ.</p>
          <button className="btn btn--primary btn--lg" style={{marginTop:22}} onClick={()=>go("listing")}>
            تصفّح المتجر <Icon name="arrowL" size={18} />
          </button>
        </div>
      </main>
    );
  }

  // --- Order summary (cart + checkout) ---
  const Summary = (
    <div className="summary">
      <h3>ملخّص الطلب</h3>
      <div className="sumrow"><span>المجموع الفرعي ({count} قطعة)</span><span className="num">{fmt(subtotal)} د.ا</span></div>
      <div className="sumrow"><span>الشحن</span><span>{shipping===0? <b style={{color:"var(--green-strong)"}}>مجاني</b> : <span className="num">{fmt(shipping)} د.ا</span>}</span></div>
      {stage==="cart" && (
        <div className="promo">
          <input value={promo} onChange={e=>setPromo(e.target.value)} placeholder="رمز الخصم" />
          <button className="btn btn--dark btn--sm">تطبيق</button>
        </div>
      )}
      <div className="sumrow total"><span>الإجمالي</span><Price now={total} /></div>
      {stage==="cart"
        ? <button className="btn btn--primary btn--block btn--lg" style={{marginTop:18}} onClick={()=>setStage("checkout")}>متابعة الدفع <Icon name="arrowL" size={18} /></button>
        : <button className="btn btn--primary btn--block btn--lg" style={{marginTop:18}} onClick={()=>setStage("done")}><Icon name="lock" size={18} /> تأكيد الطلب والدفع</button>
      }
      <div className="note"><Icon name="hands" size={18} /><span>١٠٠٪ من العائد يُوجَّه لدعم تأهيل النزلاء وإعادة دمجهم.</span></div>
      {shipping>0 && <p className="muted center" style={{fontSize:".82rem",marginTop:12}}>أضف بقيمة {fmt(50-subtotal)} د.ا للحصول على شحن مجاني</p>}
    </div>
  );

  // --- Cart + Checkout layout ---
  return (
    <main className="wrap" style={{paddingTop:30}}>
      {Steps}
      <div className="cartwrap">
        <div>
          {stage==="cart" ? (
            <div className="formcard" style={{padding:"6px 26px"}}>
              {lines.map(l=>(
                <div className="citem" key={l.id}>
                  <Photo tone={l.tone} label="" className="citem__media" />
                  <div className="citem__body">
                    <h3 style={{cursor:"pointer"}} onClick={()=>go("product",{id:l.id})}>{l.name}</h3>
                    <span className="pcard__center"><Icon name="pin" />{CENTERS[l.center]}</span>
                    <button className="citem__rm" onClick={()=>cart.removeFromCart(l.id)}><Icon name="trash" size={15} /> إزالة</button>
                  </div>
                  <div className="citem__end">
                    <Price now={l.price*l.q} />
                    <div className="qty">
                      <button onClick={()=>cart.setQty(l.id,l.q-1)} aria-label="إنقاص"><Icon name="minus" size={16} /></button>
                      <span className="num">{l.q}</span>
                      <button onClick={()=>cart.setQty(l.id,l.q+1)} aria-label="زيادة"><Icon name="plus" size={16} /></button>
                    </div>
                  </div>
                </div>
              ))}
              <div style={{padding:"18px 0"}}>
                <button className="link-btn" onClick={()=>go("listing")}><Icon name="arrowR" size={15} style={{verticalAlign:"-2px"}} /> متابعة التسوّق</button>
              </div>
            </div>
          ) : (
            <React.Fragment>
              <div className="formcard">
                <h3><span className="ic"><Icon name="user" size={18} /></span> معلومات التواصل</h3>
                <div className="field2">
                  <div className="field"><label>الاسم الكامل</label><input placeholder="الاسم الكامل" /></div>
                  <div className="field"><label>رقم الهاتف</label><input placeholder="٠٧ ____ ____" /></div>
                </div>
                <div className="field"><label>البريد الإلكتروني</label><input type="email" placeholder="name@example.com" /></div>
              </div>

              <div className="formcard">
                <h3><span className="ic"><Icon name="truck" size={18} /></span> عنوان التوصيل</h3>
                <div className="field2">
                  <div className="field"><label>المحافظة</label>
                    <select><option>عمّان</option><option>إربد</option><option>الزرقاء</option><option>العقبة</option><option>الكرك</option><option>معان</option></select>
                  </div>
                  <div className="field"><label>المنطقة</label><input placeholder="المنطقة / الحي" /></div>
                </div>
                <div className="field"><label>العنوان التفصيلي</label><input placeholder="الشارع، رقم المبنى، الطابق" /></div>
                <div className="field"><label>ملاحظات (اختياري)</label><textarea rows="2" placeholder="أي تفاصيل تساعد المندوب"></textarea></div>
              </div>

              <div className="formcard">
                <h3><span className="ic"><Icon name="lock" size={18} /></span> طريقة الدفع</h3>
                <label className={"payopt"+(pay==="card"?" is-on":"")}>
                  <input type="radio" name="pay" checked={pay==="card"} onChange={()=>setPay("card")} />
                  <span style={{flex:1}}><b>بطاقة ائتمان / مدى</b><br/><span>Visa · Mastercard — دفع آمن ومشفّر</span></span>
                  <Icon name="lock" size={18} />
                </label>
                <label className={"payopt"+(pay==="cliq"?" is-on":"")}>
                  <input type="radio" name="pay" checked={pay==="cliq"} onChange={()=>setPay("cliq")} />
                  <span style={{flex:1}}><b>CliQ / eFAWATEERcom</b><br/><span>تحويل فوري عبر تطبيق بنكك</span></span>
                  <Icon name="phone" size={18} />
                </label>
                <label className={"payopt"+(pay==="cod"?" is-on":"")}>
                  <input type="radio" name="pay" checked={pay==="cod"} onChange={()=>setPay("cod")} />
                  <span style={{flex:1}}><b>الدفع عند الاستلام</b><br/><span>ادفع نقدًا عند وصول الطلب</span></span>
                  <Icon name="truck" size={18} />
                </label>
                <button className="link-btn" style={{marginTop:8}} onClick={()=>setStage("cart")}>
                  <Icon name="arrowR" size={15} style={{verticalAlign:"-2px"}} /> العودة إلى السلة
                </button>
              </div>
            </React.Fragment>
          )}
        </div>
        {Summary}
      </div>
    </main>
  );
}
```

### Key CSS
```css
.cartwrap{ display:grid; grid-template-columns:1fr 380px; gap:34px; padding-block:36px; align-items:start; }
.steps{ display:flex; align-items:center; gap:8px; margin-bottom:26px; }
.step{ display:flex; align-items:center; gap:9px; color:var(--titanium-mute); font-weight:600; }
.step.is-on{ color:var(--titanium); } .step.is-done{ color:var(--green-strong); }
.step .n{ width:30px; height:30px; border-radius:50%; border:2px solid currentColor; display:flex; align-items:center; justify-content:center; }
.step.is-on .n{ background:var(--navy); border-color:var(--navy); color:#fff; }
.step.is-done .n{ background:var(--green); border-color:var(--green); color:#fff; }
.steps .bar{ flex:1; height:2px; background:var(--line-strong); min-width:20px; }
.citem{ display:grid; grid-template-columns:110px 1fr auto; gap:18px; padding:18px 0; border-bottom:1px solid var(--line); align-items:center; }
.citem__media{ width:110px; height:110px; border-radius:14px; }
.citem__end{ display:flex; flex-direction:column; align-items:flex-end; gap:14px; }
.summary{ background:#fff; border:1px solid var(--line); border-radius:20px; padding:24px; position:sticky; top:calc(var(--nav-h) + 16px); }
.sumrow{ display:flex; justify-content:space-between; align-items:center; padding:9px 0; color:var(--titanium-soft); }
.sumrow.total{ border-top:1px solid var(--line); margin-top:10px; padding-top:16px; font-size:1.25rem; font-weight:700; color:var(--titanium); }
.promo{ display:flex; gap:8px; margin:16px 0; }
.note{ display:flex; gap:9px; align-items:flex-start; background:var(--green-soft); border-radius:10px; padding:12px 14px; margin-top:16px; color:var(--green-strong); }
.field label{ display:block; font-size:.88rem; font-weight:600; margin-bottom:7px; }
.field input,.field select,.field textarea{ width:100%; border:1.5px solid var(--line); border-radius:10px; padding:12px 14px; }
.field input:focus,.field select:focus,.field textarea:focus{ border-color:var(--navy); }
.field2{ display:grid; grid-template-columns:1fr 1fr; gap:14px; }
.payopt{ display:flex; align-items:center; gap:12px; border:1.5px solid var(--line); border-radius:14px; padding:15px 16px; cursor:pointer; margin-bottom:10px; }
.payopt.is-on{ border-color:var(--green); background:var(--green-soft); }
.confirm{ text-align:center; max-width:560px; margin-inline:auto; padding-block:60px; }
.confirm .ic{ width:96px; height:96px; border-radius:50%; background:var(--green); color:#fff; display:flex; align-items:center; justify-content:center; margin:0 auto 26px; box-shadow:var(--sh-green); }
@media (max-width:980px){ .cartwrap{ grid-template-columns:1fr; } .summary{ position:static; } }
@media (max-width:600px){
  .citem{ grid-template-columns:90px 1fr; } .citem__media{ width:90px; height:90px; }
  .citem__end{ grid-column:1/-1; flex-direction:row; align-items:center; justify-content:space-between; }
  .field2{ grid-template-columns:1fr; } .step span:not(.n){ display:none; }
}
```

---

## Build checklist
- [ ] RTL layout; IBM Plex Sans Arabic; sits between navbar and footer.
- [ ] Steps indicator (السلة → الدفع → التأكيد) on all stages, with on/done states.
- [ ] Empty-cart state (icon + message + "تصفّح المتجر").
- [ ] Cart stage: line items (photo, title→product, center, remove, line price, qty stepper) + "متابعة التسوّق" link.
- [ ] Sticky order summary: subtotal, shipping (free over 50 د.ا), promo (cart only), total, mission note, free-shipping nudge; stage-correct CTA.
- [ ] Checkout stage: 3 form cards (contact / address / payment) + payment options (card/CliQ/COD) + back-to-cart link.
- [ ] Confirmation stage: green check, order recap card (#SB-2026-0418, delivery, payment, total), clear-cart buttons.
- [ ] All Arabic text verbatim; prices `د.ا` 3 decimals; Arabic-Indic numerals; totals logic matches.
- [ ] Responsive collapses per the table.
```
```
