import type { Product } from "../../types/Product";

type ProductsViewProps = {
    products: Product[];
    query: string;
    onQueryChange: (value: string) => void;
    onDelete: (id: string) => void;
};

export function ProductsView({
                                 products,
                                 query,
                                 onQueryChange,
                                 onDelete,
                             }: ProductsViewProps) {
    return (
        <div>
            <h2>Products</h2>

            {/* Search */}
            <input
                type="text"
                placeholder="Search products…"
                value={query}
                onChange={(e) => {
                    console.log("QUERY:", e.target.value);
                    onQueryChange(e.target.value)}
            }
                style={{ marginBottom: 12 }}
            />

            {products.length === 0 && <p>No products</p>}

            <ul>
                {products.map((p) => (
                    <li key={p.id} style={{ marginBottom: 8 }}>
                        <strong>{p.name}</strong> — {p.expiry}

                        <button
                            title={"DELETE CLICK"}
                            style={{ marginLeft: 12 }}
                            onClick={() => {
                                console.log("DELETE CLICK", p.id, p);
                                
                                onDelete(p.id);}}
                        >
                            {"Delete"}
                        </button>
                    </li>
                ))}
            </ul>
        </div>
    );
}
