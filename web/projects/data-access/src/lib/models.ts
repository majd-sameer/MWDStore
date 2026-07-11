/**
 * TypeScript models for the Store.Api surface.
 *
 * Generated to mirror the OpenAPI document at `/swagger/v1/swagger.json`
 * (components.schemas). Conventions:
 *   - OpenAPI `integer` (int32/int64) and `number` (double) -> `number`
 *   - `string` with `format: date-time` -> ISO `string`
 *   - OpenAPI `nullable: true` -> `| null`
 *   - properties absent from a schema's `required` list on request bodies -> optional (`?`)
 *
 * This file is framework-pure: plain interfaces, no Angular imports.
 */

// ---------------------------------------------------------------------------
// Auth & account
// ---------------------------------------------------------------------------

export interface RegisterRequest {
  email: string;
  password: string;
  fullName?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface AuthResponse {
  accessToken: string | null;
  expiresAt: string;
  userId: number;
  email: string | null;
  fullName?: string | null;
  /** `true` when the credentials were valid but a TOTP code is still required. */
  mfaRequired?: boolean;
  /** Short-lived token to present to `/api/auth/mfa/verify` together with the code. */
  challengeToken?: string | null;
}

export interface MfaVerifyRequest {
  challengeToken: string;
  /** Current authenticator code — or an unused recovery code. */
  code: string;
}

export interface MfaStatusResponse {
  enabled: boolean;
}

export interface MfaSetupResponse {
  sharedKey: string | null;
  authenticatorUri: string | null;
}

export interface MfaEnableRequest {
  code: string;
}

export interface MfaEnableResponse {
  recoveryCodes: string[] | null;
}

export interface MfaDisableRequest {
  code: string;
}

export interface AccountProfile {
  id: number;
  email: string | null;
  userName: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[] | null;
}

export interface UpdateProfileRequest {
  fullName?: string | null;
  phoneNumber?: string | null;
}

// ---------------------------------------------------------------------------
// Catalog (storefront reads)
// ---------------------------------------------------------------------------

export interface CalculatedProductPrice {
  price: number;
  oldPrice: number | null;
  percentOfSaving: number;
}

export interface BrandInfo {
  id: number;
  name: string | null;
  slug: string | null;
}

export interface BrandDto {
  id: number;
  name: string | null;
  slug: string | null;
}

export interface CategoryDto {
  id: number;
  name: string | null;
  slug: string | null;
  parentId: number | null;
  displayOrder: number;
  includeInMenu: boolean;
}

export interface ProductListItem {
  id: number;
  name: string | null;
  slug: string | null;
  price: number;
  oldPrice: number | null;
  specialPrice: number | null;
  specialPriceStart: string | null;
  specialPriceEnd: string | null;
  isCallForPricing: boolean;
  isAllowToOrder: boolean;
  stockQuantity: number | null;
  reviewsCount: number;
  ratingAverage: number | null;
  thumbnailImageUrl: string | null;
  shortDescription: string | null;
  /** First category name — card eyebrow / list-row label. */
  categoryName: string | null;
  /** First category slug — used to translate the category label by slug. */
  categorySlug: string | null;
  calculatedProductPrice: CalculatedProductPrice;
}

export interface FilterPrice {
  minPrice: number;
  maxPrice: number;
}

export interface FilterCategory {
  id: number;
  name: string | null;
  slug: string | null;
  parentId: number | null;
  count: number;
}

export interface FilterBrand {
  id: number;
  name: string | null;
  slug: string | null;
  count: number;
}

export interface FilterOption {
  /** Distinct product count of the unfiltered base query. */
  total: number;
  price: FilterPrice;
  categories: FilterCategory[] | null;
  brands: FilterBrand[] | null;
}

export interface ProductListResult {
  products: ProductListItem[] | null;
  totalProduct: number;
  page: number;
  pageSize: number;
  filterOption: FilterOption;
}

export interface ProductDetailAttribute {
  name: string | null;
  value: string | null;
}

export interface ProductDetailCategory {
  id: number;
  name: string | null;
  slug: string | null;
}

export interface ProductDetailVariationOption {
  optionId: number;
  optionName: string | null;
  value: string | null;
}

export interface ProductDetailVariation {
  id: number;
  name: string | null;
  normalizedName: string | null;
  isCallForPricing: boolean;
  isAllowToOrder: boolean;
  stockQuantity: number;
  stockTrackingIsEnabled: boolean;
  thumbnailImageUrl: string | null;
  imageUrls: string[] | null;
  calculatedProductPrice: CalculatedProductPrice;
  options: ProductDetailVariationOption[] | null;
}

export interface ProductDetailModel {
  id: number;
  name: string | null;
  brand: BrandInfo;
  calculatedProductPrice: CalculatedProductPrice;
  isCallForPricing: boolean;
  isAllowToOrder: boolean;
  stockTrackingIsEnabled: boolean;
  stockQuantity: number;
  shortDescription: string | null;
  description: string | null;
  specification: string | null;
  metaTitle: string | null;
  metaKeywords: string | null;
  metaDescription: string | null;
  reviewsCount: number;
  ratingAverage: number | null;
  thumbnailImageUrl: string | null;
  imageUrls: string[] | null;
  attributes: ProductDetailAttribute[] | null;
  categories: ProductDetailCategory[] | null;
  variations: ProductDetailVariation[] | null;
  relatedProducts: ProductListItem[] | null;
  crossSellProducts: ProductListItem[] | null;
}

// ---------------------------------------------------------------------------
// Cart
// ---------------------------------------------------------------------------

export interface CartItemModel {
  id: number;
  productId: number;
  productName: string | null;
  productImageUrl: string | null;
  productPrice: number;
  calculatedProductPrice: CalculatedProductPrice;
  quantity: number;
  productStockQuantity: number;
  productStockTrackingIsEnabled: boolean;
  isProductAvailableToOrder: boolean;
}

export interface CartModel {
  customerId: number;
  couponCode: string | null;
  couponValidationErrorMessage: string | null;
  items: CartItemModel[] | null;
  subTotal: number;
  discount: number;
}

export interface AddToCartRequest {
  productId: number;
  quantity?: number;
}

export interface UpdateCartItemRequest {
  quantity?: number;
}

// ---------------------------------------------------------------------------
// Checkout & orders
// ---------------------------------------------------------------------------

export interface AddressDto {
  contactName?: string | null;
  phone?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  zipCode?: string | null;
  districtId?: number | null;
  stateOrProvinceId?: number;
  countryId?: string | null;
}

export interface ShippingOptionsRequest {
  shippingAddress: AddressDto;
  couponCode?: string | null;
}

export interface ShippingOptionDto {
  /** Provider (carrier) id, e.g. `Aramex` / `JordanPost` — used to localize the label. */
  id: string | null;
  name: string | null;
  price: number;
}

/** An enabled payment method offered at checkout (no gateway credentials). */
export interface PaymentMethodDto {
  /** Provider id (e.g. `CoD`, `Stripe`, `MEPS`) — sent back as the order's payment method. */
  id: string;
  /** Display name shown to the shopper. */
  name: string | null;
}

/** Request to start a redirect-gateway payment (Stripe / PayPal / MEPS). */
export interface PaymentInitiateRequest {
  orderId: number;
  /** Provider id handling the payment. */
  method: string;
  /** Where the gateway should send the shopper back after paying. */
  returnUrl: string;
}

/** Where to send the shopper after initiating a payment. */
export interface GatewayInitiationResult {
  paymentId: number;
  orderId: number;
  method: string;
  /** Hosted-page URL (a sandbox simulation when testing). */
  redirectUrl: string;
  /** True when the simulated sandbox flow is in effect. */
  isSandbox: boolean;
}

/** Callback payload settling a gateway payment (the gateway, or the sandbox mock page, posts this). */
export interface GatewayCallbackRequest {
  orderId: number;
  method: string;
  /** Gateway result code (e.g. `APPROVED` / `DECLINED`). */
  result: string;
  gatewayTransactionId?: string | null;
  signature?: string | null;
}

/** Outcome of processing a gateway callback. */
export interface GatewayPaymentResult {
  paymentId: number;
  orderId: number;
  approved: boolean;
  gatewayTransactionId: string | null;
}

/** Request to settle a Stripe Checkout payment from its session id (the storefront return page posts this). */
export interface StripeVerifyRequest {
  /** The Stripe Checkout Session id (`cs_test_…`) Stripe appends to the success URL. */
  sessionId: string;
}

export interface PlaceOrderRequest {
  shippingAddress: AddressDto;
  billingAddress?: AddressDto;
  shippingMethodName: string;
  paymentMethod?: string | null;
  paymentFeeAmount?: number;
  couponCode?: string | null;
  orderNote?: string | null;
  isProductPriceIncludeTax?: boolean;
}

/** A single cart line a guest checks out with (guests have no server cart — they post the lines). */
export interface GuestCartLine {
  productId: number;
  quantity: number;
}

/** Guest variant of {@link ShippingOptionsRequest} — carries the cart lines in the body. */
export interface GuestShippingOptionsRequest {
  shippingAddress: AddressDto;
  items: GuestCartLine[];
}

/** Guest variant of {@link PlaceOrderRequest} — carries the cart lines and the optional contact email. */
export interface GuestPlaceOrderRequest {
  /** Optional; when omitted/blank the backend synthesizes a unique placeholder. */
  email?: string | null;
  items: GuestCartLine[];
  shippingAddress: AddressDto;
  billingAddress?: AddressDto;
  shippingMethodName: string;
  paymentMethod?: string | null;
  paymentFeeAmount?: number;
  orderNote?: string | null;
  isProductPriceIncludeTax?: boolean;
}

/** Request to start a redirect-gateway payment for a guest order (validated by `email`). */
export interface GuestPaymentInitiateRequest {
  orderId: number;
  method: string;
  returnUrl: string;
  /** The email the order was placed under (must match the order's guest email). */
  email: string;
}

export interface OrderAddressDto {
  contactName: string | null;
  phone: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  zipCode: string | null;
  stateOrProvinceId: number;
  countryId: string | null;
}

export interface OrderItemDto {
  id: number;
  productId: number;
  productName: string | null;
  productPrice: number;
  quantity: number;
  discountAmount: number;
  taxAmount: number;
  taxPercent: number;
}

export interface OrderSummaryDto {
  id: number;
  /** Public 6-digit code customers use to track the order. */
  trackingNumber: string | null;
  createdOn: string;
  orderStatus: number;
  orderStatusName: string | null;
  orderTotal: number;
  itemCount: number;
}

/** A single status-change milestone for the tracking timeline. */
export interface OrderTrackingEventDto {
  status: number;
  statusName: string | null;
  createdOn: string;
}

/** Public order-status view returned by the anonymous tracking lookup. */
export interface OrderTrackingDto {
  id: number;
  /** Public 6-digit code customers use to track the order. */
  trackingNumber: string | null;
  createdOn: string;
  orderStatus: number;
  orderStatusName: string | null;
  orderTotal: number;
  itemCount: number;
  shippingMethod: string | null;
  paymentMethod: string | null;
  history: OrderTrackingEventDto[];
  /** Full order view (same as the signed-in customer's), gated by the email match. */
  detail: OrderDetailDto;
}

export interface OrderDetailDto {
  id: number;
  /** Public 6-digit code customers use to track the order. */
  trackingNumber: string | null;
  createdOn: string;
  orderStatus: number;
  orderStatusName: string | null;
  customerId: number;
  couponCode: string | null;
  subTotal: number;
  subTotalWithDiscount: number;
  discountAmount: number;
  taxAmount: number;
  shippingMethod: string | null;
  shippingFeeAmount: number;
  paymentMethod: string | null;
  paymentFeeAmount: number;
  orderTotal: number;
  orderNote: string | null;
  shippingAddress: OrderAddressDto;
  billingAddress: OrderAddressDto;
  items: OrderItemDto[] | null;
  /** Guest contact email / order secret — set for the placing client only, null in public tracking. */
  guestEmail: string | null;
  /** Captured/refunded totals for the refund panel — admin order detail only, null elsewhere. */
  paymentSummary?: OrderPaymentSummaryDto | null;
}

// ---------------------------------------------------------------------------
// Admin
// ---------------------------------------------------------------------------

export interface AdminBrandDto {
  id: number;
  name: string | null;
  slug: string | null;
  description: string | null;
  isPublished: boolean;
  isDeleted: boolean;
}

export interface BrandUpsertRequest {
  name: string;
  slug?: string | null;
  description?: string | null;
  isPublished?: boolean;
}

export interface AdminCategoryDto {
  id: number;
  name: string | null;
  slug: string | null;
  description: string | null;
  displayOrder: number;
  isPublished: boolean;
  includeInMenu: boolean;
  parentId: number | null;
  isDeleted: boolean;
}

export interface CategoryUpsertRequest {
  name: string;
  slug?: string | null;
  description?: string | null;
  metaTitle?: string | null;
  metaKeywords?: string | null;
  metaDescription?: string | null;
  displayOrder?: number;
  isPublished?: boolean;
  includeInMenu?: boolean;
  parentId?: number | null;
}

export interface MediaDto {
  id: number;
  fileName: string | null;
  url: string;
  caption: string | null;
  mediaType: number;
}

export interface MediaListItemDto {
  id: number;
  fileName: string | null;
  url: string;
  caption: string | null;
  mediaType: number;
  fileSize: number;
  /** How many products/categories/brands reference this file (0 = safe to delete). */
  referenceCount: number;
}

export interface MediaListResult {
  items: MediaListItemDto[] | null;
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface RefundOrderRequest {
  /** Omit for a full refund of the remaining refundable balance. */
  amount?: number | null;
  reason?: string | null;
  /** Client-generated key making retries safe. */
  idempotencyKey?: string | null;
}

export interface RefundResultDto {
  refundId: number;
  orderId: number;
  paymentId: number;
  amount: number;
  totalRefunded: number;
  paymentStatus: number;
  fullyRefunded: boolean;
  providerRefundId: string | null;
  alreadyProcessed: boolean;
}

export interface OrderPaymentSummaryDto {
  capturedTotal: number;
  refundedTotal: number;
  /** Remaining balance a refund may claim (captured − refunded). */
  refundable: number;
}

export interface AdminProductListItem {
  id: number;
  name: string | null;
  slug: string | null;
  price: number;
  oldPrice: number | null;
  stockQuantity: number;
  isPublished: boolean;
  isDeleted: boolean;
  brandId: number | null;
  hasOptions: boolean;
  isVisibleIndividually: boolean;
  thumbnailUrl: string | null;
}

/** One option value; the backend stores the list as JSON (PascalCase keys for old-data compat). */
export interface ProductOptionValueItem {
  key: string;
  /** Presentation value, e.g. a hex color when displayType is "color". */
  display?: string | null;
}

export interface AdminProductMediaDto {
  mediaId: number;
  url: string;
  caption: string | null;
  mediaType: number;
}

export interface AdminProductOptionDto {
  optionId: number;
  name: string | null;
  displayType: string | null;
  values: ProductOptionValueItem[];
}

export interface AdminProductOptionCombinationDto {
  optionId: number;
  optionName: string | null;
  value: string | null;
  sortIndex: number;
}

export interface AdminProductVariationDto {
  id: number;
  name: string | null;
  sku: string | null;
  gtin: string | null;
  price: number;
  oldPrice: number | null;
  thumbnailImageId: number | null;
  thumbnailUrl: string | null;
  media: AdminProductMediaDto[];
  optionCombinations: AdminProductOptionCombinationDto[];
}

export interface AdminProductLinkDto {
  id: number;
  name: string | null;
  isPublished: boolean;
}

export interface AdminProductAttributeValueDto {
  attributeId: number;
  name: string | null;
  groupName: string | null;
  value: string | null;
}

export interface ProductQuickSearchItem {
  id: number;
  name: string | null;
  sku: string | null;
  isPublished: boolean;
}

export interface AdminProductDetail {
  id: number;
  name: string | null;
  slug: string | null;
  shortDescription: string | null;
  description: string | null;
  specification: string | null;
  metaTitle: string | null;
  metaKeywords: string | null;
  metaDescription: string | null;
  price: number;
  oldPrice: number | null;
  specialPrice: number | null;
  specialPriceStart: string | null;
  specialPriceEnd: string | null;
  sku: string | null;
  gtin: string | null;
  isPublished: boolean;
  isFeatured: boolean;
  isAllowToOrder: boolean;
  isCallForPricing: boolean;
  stockTrackingIsEnabled: boolean;
  stockQuantity: number;
  displayOrder: number;
  brandId: number | null;
  taxClassId: number | null;
  isDeleted: boolean;
  categoryIds: number[] | null;
  thumbnailImageId: number | null;
  thumbnailUrl: string | null;
  media: AdminProductMediaDto[];
  attributes: AdminProductAttributeValueDto[];
  options: AdminProductOptionDto[];
  variations: AdminProductVariationDto[];
  relatedProducts: AdminProductLinkDto[];
  crossSellProducts: AdminProductLinkDto[];
}

export interface ProductOptionRequest {
  optionId: number;
  displayType?: string | null;
  values: ProductOptionValueItem[];
}

export interface ProductOptionCombinationRequest {
  optionId: number;
  value: string;
  sortIndex?: number;
}

export interface ProductVariationRequest {
  name: string;
  sku?: string | null;
  gtin?: string | null;
  price: number;
  oldPrice?: number | null;
  thumbnailImageId?: number | null;
  mediaIds?: number[];
  optionCombinations: ProductOptionCombinationRequest[];
}

export interface ProductAttributeValueRequest {
  attributeId: number;
  value?: string | null;
}

export interface ProductUpsertRequest {
  name: string;
  slug?: string | null;
  shortDescription?: string | null;
  description?: string | null;
  specification?: string | null;
  metaTitle?: string | null;
  metaKeywords?: string | null;
  metaDescription?: string | null;
  price?: number;
  oldPrice?: number | null;
  specialPrice?: number | null;
  specialPriceStart?: string | null;
  specialPriceEnd?: string | null;
  sku?: string | null;
  gtin?: string | null;
  isPublished?: boolean;
  isFeatured?: boolean;
  isAllowToOrder?: boolean;
  isCallForPricing?: boolean;
  stockTrackingIsEnabled?: boolean;
  stockQuantity?: number;
  displayOrder?: number;
  brandId?: number | null;
  taxClassId?: number | null;
  categoryIds?: number[] | null;
  thumbnailImageId?: number | null;
  mediaIds?: number[];
  attributes?: ProductAttributeValueRequest[];
  options?: ProductOptionRequest[];
  variations?: ProductVariationRequest[];
  relatedProductIds?: number[];
  crossSellProductIds?: number[];
}

// ----- Product options & attributes (admin CRUD) ----------------------------

export interface AdminProductOptionListItem {
  id: number;
  name: string | null;
}

export interface ProductOptionUpsertRequest {
  name: string;
}

export interface AdminProductAttributeDto {
  id: number;
  name: string | null;
  groupId: number;
  groupName: string | null;
}

export interface ProductAttributeUpsertRequest {
  name: string;
  groupId: number;
}

export interface AdminProductAttributeGroupDto {
  id: number;
  name: string | null;
}

export interface ProductAttributeGroupUpsertRequest {
  name: string;
}

export interface UpdateOrderStatusRequest {
  orderStatus: number;
}

export interface StockRowDto {
  warehouseId: number;
  warehouseName: string | null;
  quantity: number;
  reservedQuantity: number;
}

export interface ProductStockDto {
  productId: number;
  productName: string | null;
  productStockQuantity: number;
  warehouses: StockRowDto[] | null;
}

export interface StockAdjustmentRequest {
  productId: number;
  warehouseId: number;
  adjustedQuantity?: number;
  note?: string | null;
}

// ---------------------------------------------------------------------------
// Admin: tax, shipping, warehouses, locations
// ---------------------------------------------------------------------------

export interface AdminTaxClassDto {
  id: number;
  name: string | null;
}

export interface TaxClassUpsertRequest {
  name: string;
}

export interface AdminTaxRateDto {
  id: number;
  taxClassId: number;
  taxClassName: string | null;
  countryId: string | null;
  countryName: string | null;
  stateOrProvinceId: number | null;
  stateOrProvinceName: string | null;
  zipCode: string | null;
  rate: number;
}

export interface TaxRateUpsertRequest {
  taxClassId: number;
  countryId?: string | null;
  stateOrProvinceId?: number | null;
  zipCode?: string | null;
  rate: number;
}

export interface AdminShippingProviderDto {
  id: string;
  name: string | null;
  isEnabled: boolean;
  freeShippingMinimumOrderAmount: number | null;
}

export interface ShippingProviderUpdateRequest {
  name: string;
  isEnabled: boolean;
  freeShippingMinimumOrderAmount?: number | null;
}

export interface AdminTableRateDto {
  id: number;
  shippingProviderId: string | null;
  shippingProviderName: string | null;
  countryId: string | null;
  countryName: string | null;
  stateOrProvinceId: number | null;
  stateOrProvinceName: string | null;
  zipCode: string | null;
  minOrderSubtotal: number;
  shippingPrice: number;
  note: string | null;
}

export interface TableRateUpsertRequest {
  shippingProviderId: string;
  countryId?: string | null;
  stateOrProvinceId?: number | null;
  zipCode?: string | null;
  minOrderSubtotal: number;
  shippingPrice: number;
  note?: string | null;
}

export interface AdminWarehouseDto {
  id: number;
  name: string | null;
  contactName: string | null;
  phone: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  zipCode: string | null;
  stateOrProvinceId: number;
  stateOrProvinceName: string | null;
  countryId: string;
  countryName: string | null;
}

export interface WarehouseUpsertRequest {
  name: string;
  contactName?: string | null;
  phone?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  zipCode?: string | null;
  stateOrProvinceId: number;
  countryId: string;
}

export interface CountryLookupDto {
  id: string;
  name: string | null;
}

export interface StateOrProvinceLookupDto {
  id: number;
  name: string | null;
  countryId: string;
}

// ---------------------------------------------------------------------------
// Admin: promotions (cart rules / coupons)
// ---------------------------------------------------------------------------

export interface AdminCartRuleListItem {
  id: number;
  name: string | null;
  isActive: boolean;
  isCouponRequired: boolean;
  ruleToApply: string | null;
  discountAmount: number;
  startOn: string | null;
  endOn: string | null;
  couponCount: number;
  usageCount: number;
}

export interface AdminCartRuleDetail {
  id: number;
  name: string | null;
  description: string | null;
  isActive: boolean;
  startOn: string | null;
  endOn: string | null;
  isCouponRequired: boolean;
  ruleToApply: string | null;
  discountAmount: number;
  maxDiscountAmount: number | null;
  discountStep: number | null;
  usageLimitPerCoupon: number | null;
  usageLimitPerCustomer: number | null;
  couponCode: string | null;
  categoryIds: number[];
  products: AdminProductLinkDto[];
}

export interface CartRuleUpsertRequest {
  name: string;
  description?: string | null;
  isActive?: boolean;
  startOn?: string | null;
  endOn?: string | null;
  isCouponRequired?: boolean;
  ruleToApply: string;
  discountAmount: number;
  maxDiscountAmount?: number | null;
  discountStep?: number | null;
  usageLimitPerCoupon?: number | null;
  usageLimitPerCustomer?: number | null;
  couponCode?: string | null;
  categoryIds?: number[];
  productIds?: number[];
}

export interface AdminCartRuleUsageDto {
  id: number;
  cartRuleId: number;
  cartRuleName: string | null;
  couponCode: string | null;
  userId: number;
  userEmail: string | null;
  orderId: number;
  createdOn: string;
}

// ---------------------------------------------------------------------------
// Admin: users, customer groups, moderation
// ---------------------------------------------------------------------------

export interface AdminUserListItem {
  id: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  createdOn: string;
  isDeleted: boolean;
  roles: string[];
  customerGroups: string[];
}

export interface AdminUserDetail {
  id: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[];
  customerGroupIds: number[];
}

export interface AdminUserCreateRequest {
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string | null;
  roles?: string[];
  customerGroupIds?: number[];
}

export interface AdminUserUpdateRequest {
  fullName: string;
  phoneNumber?: string | null;
  roles?: string[];
  customerGroupIds?: number[];
}

export interface RoleDto {
  id: number;
  name: string | null;
}

export interface AdminCustomerListItem {
  id: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  createdOn: string;
  isDeleted: boolean;
  orderCount: number;
  totalSpent: number;
  customerGroups: string[];
}

export interface AdminCustomerDetail {
  id: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  customerGroupIds: number[];
}

export interface AdminCustomerCreateRequest {
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string | null;
  customerGroupIds?: number[];
}

export interface AdminCustomerUpdateRequest {
  fullName: string;
  phoneNumber?: string | null;
  customerGroupIds?: number[];
}

export interface AdminCustomerGroupDto {
  id: number;
  name: string | null;
  description: string | null;
  isActive: boolean;
}

export interface CustomerGroupUpsertRequest {
  name: string;
  description?: string | null;
  isActive?: boolean;
}

export interface AdminReviewDto {
  id: number;
  title: string | null;
  comment: string | null;
  rating: number;
  reviewerName: string | null;
  userEmail: string | null;
  status: number;
  createdOn: string;
  entityId: number;
  entityTypeId: string | null;
  productName: string | null;
}

export interface AdminCommentDto {
  id: number;
  commentText: string | null;
  commenterName: string | null;
  userEmail: string | null;
  status: number;
  createdOn: string;
  entityId: number;
  entityTypeId: string | null;
  parentId: number | null;
}

/** 1 = Pending, 5 = Approved, 8 = NotApproved (old SimplCommerce enum values). */
export interface ModerationStatusRequest {
  status: number;
}

// ---------------------------------------------------------------------------
// Admin: CMS pages, menus, news
// ---------------------------------------------------------------------------

export interface AdminPageDto {
  id: number;
  name: string | null;
  slug: string | null;
  body: string | null;
  metaTitle: string | null;
  metaKeywords: string | null;
  metaDescription: string | null;
  isPublished: boolean;
  publishedOn: string | null;
  createdOn: string;
}

export interface PageUpsertRequest {
  name: string;
  slug?: string | null;
  body?: string | null;
  metaTitle?: string | null;
  metaKeywords?: string | null;
  metaDescription?: string | null;
  isPublished?: boolean;
}

export interface AdminMenuItemDto {
  id: number;
  menuId: number;
  parentId: number | null;
  name: string | null;
  customLink: string | null;
  displayOrder: number;
}

export interface AdminMenuDto {
  id: number;
  name: string | null;
  isPublished: boolean;
  isSystem: boolean;
  items: AdminMenuItemDto[];
}

export interface MenuUpsertRequest {
  name: string;
  isPublished?: boolean;
}

export interface MenuItemUpsertRequest {
  name: string;
  customLink?: string | null;
  parentId?: number | null;
  displayOrder?: number;
}

export interface AdminNewsCategoryDto {
  id: number;
  name: string | null;
  slug: string | null;
  description: string | null;
  displayOrder: number;
  isPublished: boolean;
}

export interface NewsCategoryUpsertRequest {
  name: string;
  slug?: string | null;
  description?: string | null;
  displayOrder?: number;
  isPublished?: boolean;
}

export interface AdminNewsItemListItem {
  id: number;
  name: string | null;
  slug: string | null;
  isPublished: boolean;
  createdOn: string;
  thumbnailUrl: string | null;
}

export interface AdminNewsItemDetail {
  id: number;
  name: string | null;
  slug: string | null;
  shortContent: string | null;
  fullContent: string | null;
  metaTitle: string | null;
  metaKeywords: string | null;
  metaDescription: string | null;
  isPublished: boolean;
  thumbnailImageId: number | null;
  thumbnailUrl: string | null;
  categoryIds: number[];
}

export interface NewsItemUpsertRequest {
  name: string;
  slug?: string | null;
  shortContent?: string | null;
  fullContent?: string | null;
  metaTitle?: string | null;
  metaKeywords?: string | null;
  metaDescription?: string | null;
  isPublished?: boolean;
  thumbnailImageId?: number | null;
  categoryIds?: number[];
}

// ---------------------------------------------------------------------------
// Admin: payments
// ---------------------------------------------------------------------------

export interface AdminPaymentProviderDto {
  id: string;
  name: string | null;
  isEnabled: boolean;
  additionalSettings: string | null;
}

export interface PaymentProviderUpdateRequest {
  name: string;
  isEnabled: boolean;
  additionalSettings?: string | null;
}

export interface AdminPaymentDto {
  id: number;
  orderId: number;
  amount: number;
  paymentFee: number;
  paymentMethod: string | null;
  gatewayTransactionId: string | null;
  status: number;
  createdOn: string;
}

// ---------------------------------------------------------------------------
// Admin: settings, countries/states CRUD, localization
// ---------------------------------------------------------------------------

export interface AppSettingDto {
  id: string;
  value: string | null;
  module: string | null;
  isVisibleInCommonSettingPage: boolean;
}

export interface AppSettingUpdateRequest {
  settings: Record<string, string | null>;
}

export interface AdminCountryDto {
  id: string;
  name: string | null;
  code3: string | null;
  isBillingEnabled: boolean;
  isShippingEnabled: boolean;
  isCityEnabled: boolean;
  isZipCodeEnabled: boolean;
  isDistrictEnabled: boolean;
  statesCount: number;
}

export interface CountryUpsertRequest {
  id?: string | null;
  name: string;
  code3?: string | null;
  isBillingEnabled?: boolean;
  isShippingEnabled?: boolean;
  isCityEnabled?: boolean;
  isZipCodeEnabled?: boolean;
  isDistrictEnabled?: boolean;
}

export interface StateOrProvinceUpsertRequest {
  name: string;
  code?: string | null;
  type?: string | null;
}

export interface CultureDto {
  id: string;
  name: string | null;
}

export interface AdminResourceDto {
  id: number;
  key: string;
  value: string | null;
  cultureId: string;
}

export interface ResourceUpsertRequest {
  key: string;
  value?: string | null;
  cultureId: string;
}

// ---------------------------------------------------------------------------
// Admin: shipments, vendors, contacts, logs, product templates
// ---------------------------------------------------------------------------

export interface AdminShipmentItemDto {
  id: number;
  orderItemId: number;
  productId: number;
  productName: string | null;
  quantity: number;
}

export interface AdminShipmentDto {
  id: number;
  orderId: number;
  trackingNumber: string | null;
  warehouseId: number;
  warehouseName: string | null;
  createdOn: string;
  items: AdminShipmentItemDto[];
}

export interface ShipmentItemRequest {
  orderItemId: number;
  quantity: number;
}

export interface ShipmentCreateRequest {
  orderId: number;
  warehouseId: number;
  trackingNumber?: string | null;
  items: ShipmentItemRequest[];
}

export interface AdminVendorDto {
  id: number;
  name: string | null;
  slug: string | null;
  email: string | null;
  description: string | null;
  isActive: boolean;
}

export interface VendorUpsertRequest {
  name: string;
  slug?: string | null;
  email?: string | null;
  description?: string | null;
  isActive?: boolean;
}

export interface AdminContactDto {
  id: number;
  fullName: string | null;
  emailAddress: string | null;
  phoneNumber: string | null;
  address: string | null;
  content: string | null;
  contactAreaId: number;
  contactAreaName: string | null;
  createdOn: string;
}

export interface AdminContactAreaDto {
  id: number;
  name: string | null;
}

export interface ContactAreaUpsertRequest {
  name: string;
}

export interface AdminActivityDto {
  id: number;
  activityTypeId: number;
  activityTypeName: string | null;
  userId: number;
  entityId: number;
  entityTypeId: string | null;
  createdOn: string;
}

export interface AdminSearchQueryDto {
  queryText: string;
  count: number;
  latestCreatedOn: string;
}

export interface AdminProductTemplateDto {
  id: number;
  name: string | null;
  attributes: AdminProductAttributeDto[];
}

export interface ProductTemplateUpsertRequest {
  name: string;
  attributeIds?: number[];
}

// ---------------------------------------------------------------------------
// Storefront: wishlist, reviews, content, comparison, recently viewed
// ---------------------------------------------------------------------------

export interface WishListItemDto {
  id: number;
  productId: number;
  productName: string | null;
  productSlug: string | null;
  price: number;
  thumbnailUrl: string | null;
  quantity: number;
  isAvailable: boolean;
}

export interface WishListDto {
  id: number;
  items: WishListItemDto[];
}

export interface AddWishListItemRequest {
  productId: number;
  quantity?: number;
}

export interface ReviewDto {
  id: number;
  title: string | null;
  comment: string | null;
  rating: number;
  reviewerName: string | null;
  createdOn: string;
}

export interface SubmitReviewRequest {
  title?: string | null;
  comment: string;
  rating: number;
}

export interface PublicPageDto {
  name: string | null;
  slug: string | null;
  body: string | null;
  metaTitle: string | null;
  metaKeywords: string | null;
  metaDescription: string | null;
}

export interface NewsListItemDto {
  id: number;
  name: string | null;
  slug: string | null;
  shortContent: string | null;
  thumbnailUrl: string | null;
  publishedOn: string | null;
}

export interface NewsDetailDto {
  id: number;
  name: string | null;
  slug: string | null;
  shortContent: string | null;
  fullContent: string | null;
  thumbnailUrl: string | null;
  metaTitle: string | null;
  metaKeywords: string | null;
  metaDescription: string | null;
  publishedOn: string | null;
}

export interface ContactAreaPublicDto {
  id: number;
  name: string | null;
}

export interface SubmitContactRequest {
  fullName: string;
  emailAddress: string;
  phoneNumber?: string | null;
  address?: string | null;
  content: string;
  contactAreaId: number;
}

export interface ComparisonAttributeDto {
  name: string | null;
  value: string | null;
}

export interface ComparisonProductDto {
  productId: number;
  name: string | null;
  slug: string | null;
  price: number;
  thumbnailUrl: string | null;
  attributes: ComparisonAttributeDto[];
}

export interface RecentlyViewedDto {
  productId: number;
  name: string | null;
  slug: string | null;
  price: number;
  thumbnailUrl: string | null;
  latestViewedOn: string;
}

// ----- Admin dashboard / analytics ----------------------------------------------------------------

/** Headline numbers; `revenue` excludes canceled/refunded orders. */
export interface AdminDashboardKpis {
  revenue: number;
  orders: number;
  avgOrderValue: number;
  products: number;
  outOfStock: number;
}

/** One calendar day of the revenue/orders trend (gap-filled by the API). */
export interface AdminTrendPoint {
  /** ISO date `YYYY-MM-DD`. */
  date: string;
  revenue: number;
  orders: number;
}

export interface AdminStatusSlice {
  status: number;
  statusName: string;
  count: number;
  total: number;
}

export interface AdminNameCount {
  name: string;
  count: number;
}

export interface AdminChannelMix {
  guest: number;
  account: number;
}

export interface AdminStockHealth {
  outOfStock: number;
  low: number;
  healthy: number;
  totalUnits: number;
}

export interface AdminTopProduct {
  productId: number;
  name: string;
  units: number;
  revenue: number;
}

export interface AdminLowStock {
  productId: number;
  name: string;
  sku: string | null;
  quantity: number;
  reserved: number;
}

/** Aggregate decision-maker view for the admin landing page (`GET /api/admin/dashboard/stats`). */
export interface AdminDashboardDto {
  kpis: AdminDashboardKpis;
  revenueTrend: AdminTrendPoint[];
  statusFunnel: AdminStatusSlice[];
  paymentMix: AdminNameCount[];
  channelMix: AdminChannelMix;
  stockHealth: AdminStockHealth;
  topProducts: AdminTopProduct[];
  lowStock: AdminLowStock[];
  actionQueue: OrderSummaryDto[];
}

// ---------------------------------------------------------------------------
// Content blocks (admin-editable homepage copy/media)
// ---------------------------------------------------------------------------

/** Published content block, localized for the request culture (`GET /api/content/blocks`). */
export interface ContentBlockDto {
  key: string;
  title: string | null;
  text: string | null;
  imageUrl: string | null;
  linkUrl: string | null;
  linkText: string | null;
  sortOrder: number;
}

/** Admin shape: base (Arabic) fields plus the raw English overlay values. */
export interface AdminContentBlockDto {
  id: number;
  key: string;
  title: string | null;
  text: string | null;
  imageUrl: string | null;
  linkUrl: string | null;
  linkText: string | null;
  sortOrder: number;
  isPublished: boolean;
  titleEn: string | null;
  textEn: string | null;
  linkTextEn: string | null;
}

export interface ContentBlockUpdateRequest {
  title?: string | null;
  text?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  linkText?: string | null;
  sortOrder: number;
  isPublished: boolean;
  titleEn?: string | null;
  textEn?: string | null;
  linkTextEn?: string | null;
}
