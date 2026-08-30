"use client";

import { useEffect } from "react";
import { createActivityConnection } from "./signalr";

export function useLiveReload(onChange: () => void, enabled = true) {
  useEffect(() => {
    if (!enabled) return;
    const connection = createActivityConnection();
    let mounted = true;

    connection.on("ActivityLogged", () => {
      if (mounted) onChange();
    });
    connection.on("EntitiesChanged", () => {
      if (mounted) onChange();
    });

    connection.start().catch(() => {
      /* hub unavailable until API is running */
    });

    return () => {
      mounted = false;
      connection.stop();
    };
  }, [onChange, enabled]);
}
