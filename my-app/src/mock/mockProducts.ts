import type { Product } from "../types/Product";

export const mockProducts: Product[] = [
    {
        id: "p1",
        name: "Greek yogurt 2%",
        expiry: "2026-01-18",
        confidence: 0.92,
        notes: "Fridge",
        createdAt: "2026-01-11T12:10:00Z",
        notifiedForMonth: null,
    },
    {
        id: "p2",
        name: "Sunscreen SPF 50",
        expiry: "2026-02-10",
        confidence: 0.84,
        notes: "Bathroom cabinet",
        createdAt: "2026-01-02T08:05:00Z",
        notifiedForMonth: "2026-01",
    },
    {
        id: "p3",
        name: "Coffee beans 1kg",
        expiry: "2026-04-01",
        confidence: 0.77,
        notes: "Pantry",
        createdAt: "2025-12-20T10:00:00Z",
        notifiedForMonth: null,
    },
];
