import React, { useEffect, useState } from 'react';
import { Box, Typography, Grid, Card, CardContent } from '@mui/material';
import ApiClient from '../services/ApiClient';

interface ProcessMimicProps {
  sessionId: string;
}

interface ProcessTag {
  name: string;
  path: string;
  value: number | null;
  unit: string;
  color: string;
}

const ProcessMimic: React.FC<ProcessMimicProps> = ({ sessionId }) => {
  const [tags, setTags] = useState<ProcessTag[]>([
    { name: 'Feed Temperature', path: 'Streams.Feed.Temperature', value: null, unit: 'K', color: '#4caf50' },
    { name: 'Feed Flow', path: 'Streams.Feed.Flow', value: null, unit: 'kg/s', color: '#2196f3' },
    { name: 'Top Temperature', path: 'Streams.Top.Temperature', value: null, unit: 'K', color: '#f44336' },
    { name: 'Bottom Temperature', path: 'Streams.Bottom.Temperature', value: null, unit: 'K', color: '#ff9800' },
    { name: 'Reflux Ratio', path: 'Units.Column1.RefluxRatio', value: null, unit: '-', color: '#9c27b0' },
    { name: 'Reboiler Duty', path: 'Units.Column1.ReboilerDuty', value: null, unit: 'kW', color: '#e91e63' },
  ]);

  useEffect(() => {
    const fetchTags = async () => {
      const updatedTags = await Promise.all(
        tags.map(async (tag) => {
          try {
            const response = await ApiClient.readTag(sessionId, tag.path);
            return { ...tag, value: typeof response.value === 'number' ? response.value : null };
          } catch (error) {
            console.error(`Failed to read tag ${tag.path}:`, error);
            return tag;
          }
        })
      );
      setTags(updatedTags);
    };

    fetchTags();
    const interval = setInterval(fetchTags, 2000);

    return () => clearInterval(interval);
  }, [sessionId]);

  return (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      {/* Simple SVG representation */}
      <Box sx={{ flex: 1, display: 'flex', justifyContent: 'center', alignItems: 'center', mb: 2 }}>
        <svg width="300" height="250" viewBox="0 0 300 250">
          {/* Column */}
          <rect x="120" y="40" width="60" height="160" fill="#424242" stroke="#fff" strokeWidth="2" />

          {/* Feed line */}
          <line x1="60" y1="120" x2="120" y2="120" stroke="#4caf50" strokeWidth="3" />
          <text x="50" y="115" fill="#4caf50" fontSize="12">Feed</text>

          {/* Top product */}
          <line x1="150" y1="40" x2="150" y2="10" stroke="#f44336" strokeWidth="3" />
          <line x1="150" y1="10" x2="240" y2="10" stroke="#f44336" strokeWidth="3" />
          <text x="200" y="8" fill="#f44336" fontSize="12">Top</text>

          {/* Bottom product */}
          <line x1="150" y1="200" x2="150" y2="230" stroke="#ff9800" strokeWidth="3" />
          <line x1="150" y1="230" x2="240" y2="230" stroke="#ff9800" strokeWidth="3" />
          <text x="200" y="245" fill="#ff9800" fontSize="12">Bottom</text>

          {/* Reboiler */}
          <circle cx="150" cy="215" r="12" fill="#e91e63" stroke="#fff" strokeWidth="2" />
          <text x="165" y="220" fill="#e91e63" fontSize="10">RB</text>

          {/* Condenser */}
          <rect x="135" y="20" width="30" height="15" fill="#2196f3" stroke="#fff" strokeWidth="2" />
          <text x="170" y="30" fill="#2196f3" fontSize="10">CD</text>
        </svg>
      </Box>

      {/* Tag values grid */}
      <Grid container spacing={1}>
        {tags.map((tag, index) => (
          <Grid item xs={6} md={4} key={index}>
            <Card sx={{ bgcolor: 'background.default' }}>
              <CardContent sx={{ p: 1, '&:last-child': { pb: 1 } }}>
                <Typography variant="caption" display="block" noWrap>
                  {tag.name}
                </Typography>
                <Typography variant="h6" sx={{ color: tag.color }}>
                  {tag.value !== null ? tag.value.toFixed(2) : '---'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {tag.unit}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
};

export default ProcessMimic;
