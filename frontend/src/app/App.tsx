import { Button } from "@/components/ui/button"

/**
 * Bootstrap example page. Its only purpose is to prove the toolchain works:
 * Tailwind utility classes render, the shadcn/ui Button renders, and the `@/`
 * path alias resolves. Real screens (App Shell + features) replace this in
 * later changes.
 */
function App() {
  return (
    <main className="min-h-screen flex flex-col items-center justify-center gap-6 bg-background text-foreground">
      <div className="text-center space-y-2">
        <h1 className="text-3xl font-semibold tracking-tight">
          Corporate Treasury
        </h1>
        <p className="text-muted-foreground">
          Project bootstrap — toolchain is working.
        </p>
      </div>
      <Button>shadcn/ui Button</Button>
    </main>
  )
}

export default App
