type DashboardStats = {
    total: number;
    soon: number;
    expired: number;
};

type DashboardViewProps = {
    stats: DashboardStats;
};

export function DashboardView({ stats }: DashboardViewProps) {
    return (
        <div>
            <h2>Dashboard</h2>

            <ul>
                <li>Total products: {stats.total}</li>
                <li>Expiring soon (≤ 7 days): {stats.soon}</li>
                <li>Expired: {stats.expired}</li>
            </ul>
        </div>
    );
}
