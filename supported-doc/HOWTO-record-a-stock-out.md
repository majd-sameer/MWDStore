# How to record a stock-out

*For warehouse keepers, admins and super-admins.*

When units leave a warehouse for any reason other than an online order, record a **stock-out** so the
count stays accurate and the movement is traceable.

## Steps

1. Sign in to the admin console and open **Stock management → Stock out**.
2. Enter the **product ID** and press **Look up**. The product's warehouses and on-hand quantities
   appear.
3. Choose the **warehouse** the units leave from and the **quantity** to remove (it cannot exceed the
   on-hand amount).
4. Pick a **reason**: Sale, Gift, Matched / settlement, Third party, External event, Reserved, or
   For display only.
5. If the reason is **Sale**, a **sales channel** is required (Showroom, External exhibition,
   External broker, Local broker, or Online store). It is optional for External event and Third
   party.
6. Optionally add a **recipient / reference** (broker, event or gift recipient) and a **note**.
7. Press **Record stock-out**. Stock is decreased and the movement is logged.

> **Performed by:** the person recording the stock-out is stored as the performer by default. Admins
> and super-admins may record a stock-out on someone else's behalf.

## Where to see them

- **Stock management → Stock-out log** lists every recorded stock-out. Filter by reason, channel,
  warehouse, performer and date, or **Export CSV**.
- Every stock-out also appears in **System → Audit Log** (action *Stock-out*).

Online-store orders decrease stock automatically and are stamped **Sale / Online store** — you do not
need to record those by hand.

---

# كيفية تسجيل إخراج مخزون

*لأمناء المستودعات والمدراء والمدراء العامّين.*

عند خروج وحدات من المستودع لأي سبب غير طلب إلكتروني، سجّل **إخراج مخزون** للحفاظ على دقّة العدّ
وإمكانية تتبّع الحركة.

## الخطوات

1. سجّل الدخول إلى لوحة الإدارة وافتح **إدارة المخزون ← إخراج مخزون**.
2. أدخل **معرّف المنتج** واضغط **بحث**، فتظهر مستودعات المنتج والكميات المتوفّرة.
3. اختر **المستودع** الذي تخرج منه الوحدات و**الكمية** المطلوب إخراجها (لا يمكن أن تتجاوز المتوفّر).
4. اختر **السبب**: بيع، هدية، مطابقة/تسوية، طرف ثالث، فعالية خارجية، محجوز، أو للعرض فقط.
5. إذا كان السبب **بيع**، فإن **قناة البيع** مطلوبة (صالة العرض، معرض خارجي، وسيط خارجي، وسيط محلي،
   أو المتجر الإلكتروني). وهي اختيارية للفعالية الخارجية والطرف الثالث.
6. يمكنك إضافة **المستلم / المرجع** (وسيط أو فعالية أو مستلم هدية) و**ملاحظة**.
7. اضغط **تسجيل الإخراج**. يُخصم المخزون وتُسجَّل الحركة.

> **المُنفِّذ:** يُحفظ الشخص الذي يسجّل الإخراج كمُنفِّذ افتراضيًا. ويمكن للمدير والمدير العام تسجيل
> الإخراج نيابةً عن شخص آخر.

## أين تظهر

- **إدارة المخزون ← سجل إخراج المخزون** يعرض كل عمليات الإخراج. صفِّ حسب السبب أو القناة أو المستودع
  أو المُنفِّذ أو التاريخ، أو **صدّر CSV**.
- تظهر كل عملية إخراج أيضًا في **النظام ← سجل التدقيق** (الإجراء *إخراج مخزون*).

طلبات المتجر الإلكتروني تُخصم تلقائيًا وتُوسم **بيع / المتجر الإلكتروني** — لا حاجة لتسجيلها يدويًا.
