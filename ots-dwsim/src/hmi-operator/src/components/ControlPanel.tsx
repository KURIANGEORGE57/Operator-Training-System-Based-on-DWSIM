import React, { useState } from 'react';
import {
  Grid,
  TextField,
  Button,
  Box,
  Typography,
  Alert,
  Snackbar,
} from '@mui/material';
import ApiClient from '../services/ApiClient';

interface ControlPanelProps {
  sessionId: string;
}

interface ControlPoint {
  label: string;
  tagPath: string;
  unit: string;
  min: number;
  max: number;
  step: number;
}

const controls: ControlPoint[] = [
  { label: 'Feed Temperature', tagPath: 'Streams.Feed.Temperature', unit: 'K', min: 273, max: 400, step: 1 },
  { label: 'Feed Flow Rate', tagPath: 'Streams.Feed.Flow', unit: 'kg/s', min: 0, max: 10, step: 0.1 },
  { label: 'Reflux Ratio', tagPath: 'Units.Column1.RefluxRatio', unit: '-', min: 0, max: 10, step: 0.1 },
  { label: 'Reboiler Duty', tagPath: 'Units.Column1.ReboilerDuty', unit: 'kW', min: 0, max: 1000, step: 10 },
];

const ControlPanel: React.FC<ControlPanelProps> = ({ sessionId }) => {
  const [values, setValues] = useState<Record<string, string>>({});
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false,
    message: '',
    severity: 'success',
  });

  const handleChange = (tagPath: string, value: string) => {
    setValues({ ...values, [tagPath]: value });
  };

  const handleApply = async (control: ControlPoint) => {
    const value = values[control.tagPath];
    if (!value) {
      setSnackbar({ open: true, message: 'Please enter a value', severity: 'error' });
      return;
    }

    const numValue = parseFloat(value);
    if (isNaN(numValue) || numValue < control.min || numValue > control.max) {
      setSnackbar({
        open: true,
        message: `Value must be between ${control.min} and ${control.max}`,
        severity: 'error',
      });
      return;
    }

    try {
      await ApiClient.writeTag(sessionId, control.tagPath, numValue, 'operator-001');
      setSnackbar({
        open: true,
        message: `${control.label} set to ${numValue} ${control.unit}`,
        severity: 'success',
      });
      setValues({ ...values, [control.tagPath]: '' });
    } catch (error: any) {
      setSnackbar({
        open: true,
        message: `Failed to update: ${error.message}`,
        severity: 'error',
      });
    }
  };

  const handleCloseSnackbar = () => {
    setSnackbar({ ...snackbar, open: false });
  };

  return (
    <Box>
      <Grid container spacing={2}>
        {controls.map((control, index) => (
          <Grid item xs={12} sm={6} md={3} key={index}>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              <Typography variant="subtitle2">{control.label}</Typography>
              <TextField
                size="small"
                type="number"
                value={values[control.tagPath] || ''}
                onChange={(e) => handleChange(control.tagPath, e.target.value)}
                inputProps={{
                  min: control.min,
                  max: control.max,
                  step: control.step,
                }}
                placeholder={`${control.min}-${control.max} ${control.unit}`}
              />
              <Button
                variant="contained"
                size="small"
                onClick={() => handleApply(control)}
                disabled={!values[control.tagPath]}
              >
                Apply
              </Button>
            </Box>
          </Grid>
        ))}
      </Grid>

      <Snackbar
        open={snackbar.open}
        autoHideDuration={3000}
        onClose={handleCloseSnackbar}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <Alert onClose={handleCloseSnackbar} severity={snackbar.severity} sx={{ width: '100%' }}>
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};

export default ControlPanel;
