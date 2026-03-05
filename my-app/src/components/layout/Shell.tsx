import type { ReactNode } from "react";

export type Tab = "dashboard" | "products" | "settings";

type ShellProps = {
    tab: Tab;
    onTabChange: (tab: Tab) => void;
    children: ReactNode;
};

export function Shell({ tab, onTabChange, children }: ShellProps) {
    return (
        <div style={{ padding: 40, border: "2px solid blue" }}>
            <div style={{ marginBottom: 16, fontWeight: "bold" }}>
                SHELL HEADER
            </div>

            {/* Tabs */}
            <div style={{ display: "flex", gap: 8, marginBottom: 24 }}>
                <button onClick={() => onTabChange("dashboard")}>
                    Dashboard
                </button>
                <button onClick={() => onTabChange("products")}>
                    Products
                </button>
                <button onClick={() => onTabChange("settings")}>
                    Settings
                </button>
            </div>

            <div style={{ marginBottom: 16 }}>
                Current tab: <strong>{tab}</strong>
            </div>

            {children}
        </div>
    );
}
