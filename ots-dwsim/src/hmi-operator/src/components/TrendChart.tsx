import { useState, useEffect } from 'react'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import { ApiClient } from '../services/ApiClient'

interface TrendChartProps {
  sessionId: string
}

interface DataPoint {
  time: string
  temperature: number
  pressure: number
}

export default function TrendChart({ sessionId }: TrendChartProps) {
  const [data, setData] = useState<DataPoint[]>([])

  useEffect(() => {
    const fetchData = async () => {
      try {
        const temp = await ApiClient.readTag(sessionId, 'Streams.Feed.Temperature')
        const pressure = await ApiClient.readTag(sessionId, 'Streams.Feed.Pressure')

        if (temp && pressure) {
          const now = new Date()
          const timeStr = now.toLocaleTimeString()

          setData((prevData) => {
            const newData = [
              ...prevData,
              {
                time: timeStr,
                temperature: Number(temp.value),
                pressure: Number(pressure.value),
              },
            ]

            // Keep only last 20 data points
            return newData.slice(-20)
          })
        }
      } catch (error) {
        console.error('Error fetching trend data:', error)
      }
    }

    // Fetch data every 2 seconds
    const interval = setInterval(fetchData, 2000)

    return () => clearInterval(interval)
  }, [sessionId])

  return (
    <ResponsiveContainer width="100%" height="90%">
      <LineChart data={data}>
        <CartesianGrid strokeDasharray="3 3" stroke="#444" />
        <XAxis dataKey="time" stroke="#999" style={{ fontSize: '10px' }} />
        <YAxis yAxisId="left" stroke="#8884d8" />
        <YAxis yAxisId="right" orientation="right" stroke="#82ca9d" />
        <Tooltip
          contentStyle={{ backgroundColor: '#1e1e1e', border: '1px solid #444' }}
          labelStyle={{ color: '#fff' }}
        />
        <Legend />
        <Line
          yAxisId="left"
          type="monotone"
          dataKey="temperature"
          stroke="#8884d8"
          name="Temperature (K)"
          dot={false}
        />
        <Line
          yAxisId="right"
          type="monotone"
          dataKey="pressure"
          stroke="#82ca9d"
          name="Pressure (Pa)"
          dot={false}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}
