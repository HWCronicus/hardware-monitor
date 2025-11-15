import { HardwareMonitor } from './components/HardwareMonitor'
import TopBar from './components/TopBar'

function App(): React.JSX.Element {
  return (
    <div className="flex h-screen w-screen flex-col overflow-hidden">
      <TopBar />
      <div
        className="flex flex-1 items-center justify-center"
        style={{ backgroundColor: 'rgba(17, 24, 39, 0.7)' }}
      >
        <HardwareMonitor />
      </div>
    </div>
  )
}

export default App
