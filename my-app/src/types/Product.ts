export type Product = {
    id: string;
    name: string;
    expiry: string;          // "YYYY-MM-DD"
    confidence: number;      // 0..1
    notes?: string | null;
    createdAt: string;       // ISO datetime
    notifiedForMonth?: string | null;
};

export type Tab = "dashboard" | "products" | "settings";
