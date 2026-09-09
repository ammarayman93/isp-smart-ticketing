# Power BI integration — ISP Smart Ticketing

هذا المشروع يستخدم **Power BI Embedded — App owns data**.
المستخدم يدخل إلى النظام بحساب ISP المحلي، والـ Backend هو الذي يتعامل مع Microsoft Entra ID وPower BI REST API ويعيد للواجهة Embed Token مؤقتًا. لذلك لا يتم وضع `ClientSecret` في React.

## 1. الملفات التي تمت إضافتها

### Backend
- `backend/ISP.Ticketing.Application/Common/Interfaces/IPowerBiService.cs`
- `backend/ISP.Ticketing.Application/Common/Models/PowerBiEmbedConfig.cs`
- `backend/ISP.Ticketing.Infrastructure/Services/PowerBiService.cs`
- `backend/ISP.Ticketing.API/Controllers/PowerBiController.cs`

Endpoint:

`GET /api/PowerBi/embed-config`

ومحمي للأدوار `Admin` و`Supervisor`.

### Frontend
- `frontend/src/pages/PowerBIPage.tsx`
- تمت إضافة المسار `/powerbi` وزر Power BI في شريط النظام.

الواجهة تحمل Power BI JavaScript SDK من CDN، لذلك لم نضف حزم npm جديدة ولم نكسر `package-lock.json`.

### Database / Power BI
- `database/powerbi/01_powerbi_views.sql`
- `database/powerbi/02_powerbi_dax.md`

## 2. إنشاء Microsoft Entra App

أنشئ App Registration في Microsoft Entra ID، ثم أنشئ Client Secret.
احفظ:

- Tenant ID
- Client ID
- Client Secret

لا تضع الـ Client Secret داخل React أو Git.

## 3. إعداد Power BI

أنشئ Workspace مخصص للمشروع، ثم أنشئ التقرير داخله.

في Power BI Admin Portal فعّل:

- Embed content in apps
- Allow service principals to use Power BI APIs

يفضل تقييد الإعدادات على Security Group مخصص للـ service principals.

بعدها أضف الـ service principal إلى Workspace كـ Member أو Admin.

## 4. إنشاء طبقة البيانات

نفّذ:

`database/powerbi/01_powerbi_views.sql`

على قاعدة `isp_ticketing`.

ثم افتح Power BI Desktop:

1. Get Data → MySQL database.
2. اختر قاعدة `isp_ticketing`.
3. استورد Views التالية:
   - `vw_PowerBI_Tickets`
   - `vw_PowerBI_Outages`
   - `vw_PowerBI_Classification`
   - `vw_PowerBI_TicketHistory`
   - `vw_PowerBI_Agents`
4. أنشئ Date table.
5. أضف العلاقات والمقاييس الموجودة في `database/powerbi/02_powerbi_dax.md`.
6. أنشئ التقرير بالصفحات المقترحة في الملف نفسه.
7. Publish إلى Workspace.

## 5. القيم المطلوبة من Power BI

بعد نشر التقرير، نحتاج:

- Workspace ID
- Report ID
- Dataset/Semantic Model ID (اختياري في التطبيق، لأن Backend يحاول قراءته من تقرير Power BI)

## 6. إعداد Docker

ضع القيم في `.env`:

```env
POWERBI_TENANT_ID=...
POWERBI_CLIENT_ID=...
POWERBI_CLIENT_SECRET=...
POWERBI_WORKSPACE_ID=...
POWERBI_REPORT_ID=...
POWERBI_DATASET_ID=
```

ثم:

```bash
docker compose down

docker compose build --no-cache

docker compose up -d
```

## 7. اختبار Backend

بعد تشغيل النظام، سجل الدخول كـ Admin أو Supervisor ثم افتح:

`/powerbi`

إذا كانت إعدادات Power BI صحيحة سيظهر التقرير داخل النظام.

إذا ظهر `Power BI is not configured` فهذا يعني أن إحدى قيم PowerBI ناقصة.

إذا ظهر خطأ صلاحيات من Power BI، راجع:

1. Tenant settings.
2. Service principal access.
3. Workspace role.
4. Workspace ID وReport ID.
5. أن التقرير موجود في نفس الـ Workspace المحدد.

## 8. ملاحظة مهمة حول MySQL وPower BI Service

Power BI Desktop يستطيع الاتصال بـ MySQL عبر Connector/NET. عند نشر النموذج إلى Power BI Service، تحديث البيانات من مصدر MySQL غير المتاح مباشرة للخدمة قد يتطلب On-premises Data Gateway (Standard) بحسب مكان قاعدة البيانات وطريقة الاستضافة.

للعرض التجريبي الأول للمشروع:

`MySQL → Power BI Desktop → Publish → Power BI Service → Embed داخل React`

وهذا كافٍ لإثبات التكامل الفعلي في مشروع التخرج.

للتشغيل الإنتاجي مع تحديث تلقائي:

`MySQL → Gateway / اتصال آمن → Power BI Semantic Model → Power BI Report → ISP System`
