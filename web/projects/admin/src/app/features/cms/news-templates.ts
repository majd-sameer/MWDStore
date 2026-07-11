/**
 * Predefined article layouts for the three fixed news categories. Each template is a
 * bilingual pair of HTML fragments built from `nb-*` (news-block) classes; the
 * storefront's news-detail stylesheet owns how those classes look, so editors fill in
 * text while the rendered design stays consistent. The admin rich-text editor carries
 * a lightweight preview of the same classes (see rich-text-editor.scss).
 *
 * Keep the markup shallow and execCommand-friendly: plain headings, paragraphs, lists
 * and single-level wrapper divs survive WYSIWYG editing without losing their classes.
 */
export interface NewsTemplate {
  /** Matches the seeded news-category slug (see backend NewsCategorySeeder). */
  readonly key: 'success-story' | 'activity' | 'alert';
  readonly labelKey: string;
  /** Bootstrap-icons class (without the `bi` prefix). */
  readonly icon: string;
  readonly ar: string;
  readonly en: string;
}

const SUCCESS_STORY_AR = `
<p class="nb-lead">جملة أو جملتان تلخّصان القصة وتشدّان القارئ لمتابعتها.</p>
<h2>البداية</h2>
<p>عرّف بصاحب القصة: من هو، وما الذي سعى إلى تحقيقه.</p>
<blockquote class="nb-quote">اقتباس قصير وملهم من صاحب القصة.<cite>— الاسم، الصفة</cite></blockquote>
<h2>التحدي</h2>
<p>صف الصعوبات التي واجهها وكيف تغلّب عليها.</p>
<div class="nb-highlight">
<h3>الأثر</h3>
<ul>
<li>أول نتيجة ملموسة أو إنجاز.</li>
<li>نتيجة ثانية.</li>
<li>نتيجة ثالثة.</li>
</ul>
</div>
<h2>اليوم</h2>
<p>أين وصل الآن، وما الخطوة القادمة.</p>
`.trim();

const SUCCESS_STORY_EN = `
<p class="nb-lead">One or two sentences that sum up the story and hook the reader.</p>
<h2>Where it began</h2>
<p>Introduce the person behind the story — who they are and what they set out to do.</p>
<blockquote class="nb-quote">A short, inspiring quote from the storyteller.<cite>— Name, role</cite></blockquote>
<h2>The challenge</h2>
<p>Describe the obstacles they faced and how they overcame them.</p>
<div class="nb-highlight">
<h3>The impact</h3>
<ul>
<li>First concrete result or milestone.</li>
<li>Second result.</li>
<li>Third result.</li>
</ul>
</div>
<h2>Today</h2>
<p>Where they are now, and what comes next.</p>
`.trim();

const ACTIVITY_AR = `
<p class="nb-lead">وصف موجز للنشاط وسبب أهميته.</p>
<div class="nb-facts">
<div class="nb-fact"><span class="nb-fact__label">التاريخ</span><span class="nb-fact__value">أضف التاريخ</span></div>
<div class="nb-fact"><span class="nb-fact__label">المكان</span><span class="nb-fact__value">أضف المكان</span></div>
<div class="nb-fact"><span class="nb-fact__label">المشاركون</span><span class="nb-fact__value">أضف العدد</span></div>
</div>
<h2>ماذا حدث</h2>
<p>احكِ تفاصيل النشاط من بدايته إلى نهايته.</p>
<h2>أبرز اللحظات</h2>
<ul>
<li>لحظة مميزة أولى.</li>
<li>لحظة مميزة ثانية.</li>
<li>لحظة مميزة ثالثة.</li>
</ul>
<div class="nb-highlight">
<p>اشكر المشاركين أو ادعُ القرّاء للانضمام إلى النشاط القادم.</p>
</div>
`.trim();

const ACTIVITY_EN = `
<p class="nb-lead">A short summary of the activity and why it mattered.</p>
<div class="nb-facts">
<div class="nb-fact"><span class="nb-fact__label">Date</span><span class="nb-fact__value">Add the date</span></div>
<div class="nb-fact"><span class="nb-fact__label">Location</span><span class="nb-fact__value">Add the place</span></div>
<div class="nb-fact"><span class="nb-fact__label">Participants</span><span class="nb-fact__value">Add the count</span></div>
</div>
<h2>What happened</h2>
<p>Tell the story of the activity from start to finish.</p>
<h2>Highlights</h2>
<ul>
<li>First standout moment.</li>
<li>Second standout moment.</li>
<li>Third standout moment.</li>
</ul>
<div class="nb-highlight">
<p>Thank the participants, or invite readers to join the next activity.</p>
</div>
`.trim();

const ALERT_AR = `
<div class="nb-alert">
<h2>ما يجب أن تعرفه</h2>
<p>الرسالة الأهم في جملة أو جملتين.</p>
</div>
<h2>التفاصيل</h2>
<p>اشرح الموقف بالكامل: ما الذي حدث، ومن يتأثر به، وحتى متى.</p>
<h2>ما المطلوب منك</h2>
<ol>
<li>الخطوة الأولى.</li>
<li>الخطوة الثانية.</li>
</ol>
<div class="nb-note">
<p>للاستفسار تواصل معنا عبر: أضف وسيلة التواصل.</p>
</div>
`.trim();

const ALERT_EN = `
<div class="nb-alert">
<h2>What you need to know</h2>
<p>The single most important message, in one or two sentences.</p>
</div>
<h2>Details</h2>
<p>Explain the full situation: what happened, who is affected, and until when.</p>
<h2>What to do</h2>
<ol>
<li>First step.</li>
<li>Second step.</li>
</ol>
<div class="nb-note">
<p>Questions? Reach us at: add a contact channel.</p>
</div>
`.trim();

export const NEWS_TEMPLATES: readonly NewsTemplate[] = [
  {
    key: 'success-story',
    labelKey: 'news.template.success_story',
    icon: 'bi-stars',
    ar: SUCCESS_STORY_AR,
    en: SUCCESS_STORY_EN,
  },
  {
    key: 'activity',
    labelKey: 'news.template.activity',
    icon: 'bi-calendar-event',
    ar: ACTIVITY_AR,
    en: ACTIVITY_EN,
  },
  {
    key: 'alert',
    labelKey: 'news.template.alert',
    icon: 'bi-exclamation-triangle',
    ar: ALERT_AR,
    en: ALERT_EN,
  },
];
