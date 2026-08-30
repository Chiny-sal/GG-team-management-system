"use client";

import { useParams } from "next/navigation";
import { KanbanBoard } from "@/components/KanbanBoard";

export default function BoardPage() {
  const params = useParams<{ groupId: string }>();
  return <KanbanBoard groupId={params.groupId} />;
}
