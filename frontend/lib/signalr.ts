"use client";

import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { API_URL } from "./api";

export function createActivityConnection() {
  const token = typeof window === "undefined" ? "" : window.localStorage.getItem("gg.token") ?? "";
  return new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/activity-feed`, {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
