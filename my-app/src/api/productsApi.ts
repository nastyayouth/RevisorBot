import type { Product } from "../types/Product";
import type { ExtractionDraft } from "../types/ExtractionDraft";
const API_BASE = "http://localhost:5023";

export async function fetchProducts(): Promise<Product[]> {
    const res = await fetch(`${API_BASE}/api/products`);

    if (!res.ok) {
        throw new Error("Failed to load products");
    }

    return res.json();
}

export async function getProducts(): Promise<Product[]> {
    const res = await fetch(`${API_BASE}/api/products`);
    const data = await res.json();
    console.log("Fetched products:", data);
    return data.items;
}

export async function deleteProduct(id: string): Promise<void> {
    await fetch(`${API_BASE}/api/products/${id}`, { method: "DELETE" });
}


export async function createProduct(
    draft: ExtractionDraft
): Promise<Product> {
    const res = await fetch(`${API_BASE}/api/products`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(draft),
    });

    return res.json();
}
