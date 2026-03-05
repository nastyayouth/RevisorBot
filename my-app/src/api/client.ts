export async function apiGet<T>(url: string): Promise<T> {
    const res = await fetch(url, {
        headers: {
            "Content-Type": "application/json",
        },
    });

    if (!res.ok) {
        throw new Error(`API error ${res.status}`);
    }

    return res.json() as Promise<T>;
}
