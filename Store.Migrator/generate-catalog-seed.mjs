// Converts supported-doc/psd_eshop_products.csv into Store.Api/catalog.seed.json,
// the editable source of truth consumed by CatalogSeeder at startup.
//
//   node Store.Migrator/generate-catalog-seed.mjs
//
// Mapping rules:
//   - Rows are deduped by SKU (the CSV contains exact duplicate rows); first row wins.
//   - Product slug = lowercase SKU (names are Arabic, and the source site uses the same scheme).
//   - "In stock" rows get DEFAULT_STOCK_QTY units; "Out of stock" rows get 0.
//   - image_url is kept as an absolute external URL (the media URL builders pass those through).
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const csvPath = join(root, "supported-doc", "psd_eshop_products.csv");
const outPath = join(root, "Store.Api", "catalog.seed.json");

const DEFAULT_STOCK_QTY = 10;

// Source category label -> { slug, name, displayOrder } (order = product count, descending).
const CATEGORIES = {
  "TEXTILES": { slug: "textiles", name: "Textiles", displayOrder: 1 },
  "WOODEN PRODUCTS": { slug: "wooden-products", name: "Wooden Products", displayOrder: 2 },
  "Resin products": { slug: "resin-products", name: "Resin Products", displayOrder: 3 },
  "EARTHENWARE & POTTERY": { slug: "earthenware-pottery", name: "Earthenware & Pottery", displayOrder: 4 },
  "PAINT ART": { slug: "paint-art", name: "Paint Art", displayOrder: 5 },
  "COPPERS": { slug: "coppers", name: "Coppers", displayOrder: 6 },
  "SOUVENIRS & ANTIQUES": { slug: "souvenirs-antiques", name: "Souvenirs & Antiques", displayOrder: 7 },
  "leather products": { slug: "leather-products", name: "Leather Products", displayOrder: 8 },
  "Packaged products": { slug: "packaged-products", name: "Packaged Products", displayOrder: 9 },
  "METAL PRODUCTS": { slug: "metal-products", name: "Metal Products", displayOrder: 10 },
};

// RFC 4180 CSV parser (quoted fields, escaped quotes; the file has no embedded newlines).
function parseCsv(text) {
  const rows = [];
  let row = [], field = "", inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; }
        else inQuotes = false;
      } else field += c;
    } else if (c === '"') inQuotes = true;
    else if (c === ",") { row.push(field); field = ""; }
    else if (c === "\n" || c === "\r") {
      if (c === "\r" && text[i + 1] === "\n") i++;
      row.push(field); field = "";
      if (row.some(f => f !== "")) rows.push(row);
      row = [];
    } else field += c;
  }
  if (field !== "" || row.length) { row.push(field); if (row.some(f => f !== "")) rows.push(row); }
  return rows;
}

const text = readFileSync(csvPath, "utf8").replace(/^﻿/, "");
const [header, ...rows] = parseCsv(text);
const col = Object.fromEntries(header.map((h, i) => [h.trim(), i]));
for (const required of ["category", "product_name", "price_jod", "image_url", "sku", "description", "stock_status"]) {
  if (!(required in col)) throw new Error(`CSV is missing expected column '${required}'`);
}

const products = [];
const seenSkus = new Set();
let duplicates = 0;

for (const r of rows) {
  const sku = r[col.sku].trim();
  const key = sku.toLowerCase();
  if (seenSkus.has(key)) { duplicates++; continue; }
  seenSkus.add(key);

  const category = CATEGORIES[r[col.category].trim()];
  if (!category) throw new Error(`Unknown category '${r[col.category]}' (sku ${sku})`);

  const price = Number(r[col.price_jod]);
  if (!Number.isFinite(price)) throw new Error(`Bad price '${r[col.price_jod]}' (sku ${sku})`);

  const inStock = r[col.stock_status].trim().toLowerCase() === "in stock";
  const description = r[col.description].trim();

  products.push({
    slug: key,
    sku,
    name: r[col.product_name].trim().replace(/\s+/g, " "),
    price,
    shortDescription: description,
    description,
    stock: inStock ? DEFAULT_STOCK_QTY : 0,
    categories: [category.slug],
    images: [r[col.image_url].trim()],
    isFeatured: false,
  });
}

const seed = {
  categories: Object.values(CATEGORIES)
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map(c => ({ slug: c.slug, name: c.name, parent: null, displayOrder: c.displayOrder })),
  products,
};

writeFileSync(outPath, JSON.stringify(seed, null, 2) + "\n", "utf8");
console.log(`Wrote ${outPath}: ${seed.categories.length} categories, ${products.length} products (${duplicates} duplicate SKU rows skipped).`);
