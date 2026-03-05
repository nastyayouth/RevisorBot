import { useEffect, useMemo, useState } from "react";
import { Shell } from "./components/layout/Shell";
import { DashboardView } from "./components/dashboard/DashboardView";
import { ProductsView } from "./components/products/ProductsView";
import { fetchProducts } from "./api/productsApi";
import type { Product } from "./types/Product";
import { mockProducts } from "./mock/mockProducts";


export default function RevisorWebUI() {
    const [tab, setTab] = useState<Tab>("dashboard");
    const [products,setProducts] = useState<Product[]>(mockProducts);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [query, setQuery] = useState("");


    useEffect(() => {
        fetchProducts()
            .then(setProducts)
            .catch(err => setError(err.message))
            .finally(() => setLoading(false));
    }, []);
    
    function removeProduct(id: string) {
        console.log("removeProduct called with id:", id);
        setProducts((prev) => prev.filter((p) => p.id !== id));
    }

    const filteredProducts = useMemo(() => {
        const q = query.toLowerCase().trim();
        if (!q) return products;

        return products.filter((p) =>
            p.name.toLowerCase().includes(q)
        );
    }, [products, query]);

    const stats = useMemo(() => {
        const today = new Date();

        const daysUntil = (iso: string) =>
            Math.round(
                (new Date(iso).getTime() -
                    new Date(
                        today.getFullYear(),
                        today.getMonth(),
                        today.getDate()
                    ).getTime()) / 86400000
            );

        const days = products.map((p) => daysUntil(p.expiry));

        return {
            total: products.length,
            soon: days.filter((d) => d >= 0 && d <= 7).length,
            expired: days.filter((d) => d < 0).length,
        };
    }, [products]);
    
    return (
        <Shell tab={tab} onTabChange={setTab}>
            {tab === "dashboard" && <DashboardView stats={stats} />}
            {tab === "products" && (
                <ProductsView
                    products={filteredProducts}
                    query={query}
                    onQueryChange={setQuery}
                    onDelete={removeProduct}
                />)}
        </Shell>
    );
}
