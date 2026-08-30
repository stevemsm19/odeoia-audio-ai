import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PipelineStatus } from "./PipelineStatus";

describe("PipelineStatus", () => {
  it("always shows Noise Filtering as not implemented, regardless of the other stages", () => {
    render(
      <PipelineStatus
        compressionStatus="Completed"
        transcriptionStatus="Completed"
        summaryStatus="Completed"
      />,
    );

    expect(screen.getByText("Noise Filtering")).toBeInTheDocument();
    expect(screen.getByText("Not implemented yet")).toBeInTheDocument();
  });

  it("shows 'Waiting for transcription' for Summary when transcription hasn't completed yet", () => {
    render(
      <PipelineStatus
        compressionStatus="Completed"
        transcriptionStatus="Processing"
        summaryStatus="Pending"
      />,
    );

    expect(screen.getByText("Waiting for transcription")).toBeInTheDocument();
  });

  it("shows Summary's own state once transcription has completed", () => {
    render(
      <PipelineStatus
        compressionStatus="Completed"
        transcriptionStatus="Completed"
        summaryStatus="Processing"
      />,
    );

    expect(screen.queryByText("Waiting for transcription")).not.toBeInTheDocument();
    const summaryRow = screen.getByText("Summary").closest("li");
    expect(summaryRow).toHaveTextContent("In progress");
  });

  it("renders a Failed row distinctly for a failed stage", () => {
    render(
      <PipelineStatus
        compressionStatus="Failed"
        transcriptionStatus="Pending"
        summaryStatus="Pending"
      />,
    );

    const compressionRow = screen.getByText("Compression").closest("li");
    expect(compressionRow).toHaveClass("pipeline-row--failed");
    expect(compressionRow).toHaveTextContent("Failed");
  });
});
