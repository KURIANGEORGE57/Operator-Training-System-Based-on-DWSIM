import React, { useEffect, useState } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { Box } from '@mui/material';
import ApiClient from '../services/ApiClient';

interface TrendChartProps {
  sessionId: string;
}

interface DataPoint {
  time: string;
  feedTemp: number | null;
  topTemp: number | null;
  bottomTemp: number | null;
  flow: number | null;
}

const TrendChart: React.FC<TrendChartProps> = ({ sessionId }) => {
  const [data, setData] = useState<DataPoint[]>([]);
  const maxDataPoints = 30;

  useEffect(() => {
    const fetchData = async () => {
      try {
        // Read multiple tags
        const [feedTemp, topTemp, bottomTemp, flow] = await Promise.all([
          ApiClient.readTag(sessionId, 'Streams.Feed.Temperature').catch(() => ({ value: null })),
          ApiClient.readTag(sessionId, 'Streams.Top.Temperature').catch(() => ({ value: null })),
          ApiClient.readTag(sessionId, 'Streams.Bottom.Temperature').catch(() => ({ value: null })),
          ApiClient.readTag(sessionId, 'Streams.Feed.Flow').catch(() => ({ value: null })),
        ]);

        const now = new Date();
        const timeStr = `${now.getHours()}:${now.getMinutes()}:${now.getSeconds()}`;

        const newPoint: DataPoint = {
          time: timeStr,
          feedTemp: typeof feedTemp.value === 'number' ? feedTemp.value : null,
          topTemp: typeof topTemp.value === 'number' ? topTemp.value : null,
          bottomTemp: typeof bottomTemp.value === 'number' ? bottomTemp.value : null,
          flow: typeof flow.value === 'number' ? flow.value : null,
        };

        setData((prevData) => {
          const newData = [...prevData, newPoint];
          // Keep only last maxDataPoints
          return newData.slice(-maxDataPoints);
        });
      } catch (error) {
        console.error('Failed to fetch trend data:', error);
      }
    };

    // Initial fetch
    fetchData();

    // Update every 2 seconds
    const interval = setInterval(fetchData, 2000);

    return () => clearInterval(interval);
  }, [sessionId]);

  return (
    <Box sx={{ width: '100%', height: '250px' }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#444" />
          <XAxis dataKey="time" stroke="#999" tick={{ fill: '#999' }} />
          <YAxis stroke="#999" tick={{ fill: '#999' }} />
          <Tooltip
            contentStyle={{ backgroundColor: '#1e1e1e', border: '1px solid #444' }}
            labelStyle={{ color: '#fff' }}
          />
          <Legend />
          <Line
            type="monotone"
            dataKey="feedTemp"
            stroke="#4caf50"
            name="Feed Temp (K)"
            dot={false}
            isAnimationActive={false}
          />
          <Line
            type="monotone"
            dataKey="topTemp"
            stroke="#f44336"
            name="Top Temp (K)"
            dot={false}
            isAnimationActive={false}
          />
          <Line
            type="monotone"
            dataKey="bottomTemp"
            stroke="#ff9800"
            name="Bottom Temp (K)"
            dot={false}
            isAnimationActive={false}
          />
          <Line
            type="monotone"
            dataKey="flow"
            stroke="#2196f3"
            name="Flow (kg/s)"
            dot={false}
            isAnimationActive={false}
            yAxisId="right"
            hide
          />
        </LineChart>
      </ResponsiveContainer>
    </Box>
  );
};

export default TrendChart;
