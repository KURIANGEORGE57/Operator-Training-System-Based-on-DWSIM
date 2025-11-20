import React, { useState } from 'react';
import {
  AppBar,
  Box,
  Container,
  CssBaseline,
  Tab,
  Tabs,
  Toolbar,
  Typography,
  ThemeProvider,
  createTheme,
} from '@mui/material';
import SessionMonitor from './components/SessionMonitor';
import ScenarioEditor from './components/ScenarioEditor';
import EventTimeline from './components/EventTimeline';
import AssessmentViewer from './components/AssessmentViewer';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#1976d2' },
    secondary: { main: '#dc004e' },
    background: { default: '#121212', paper: '#1e1e1e' },
  },
});

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

function TabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;
  return (
    <div role="tabpanel" hidden={value !== index} {...other}>
      {value === index && <Box sx={{ py: 3 }}>{children}</Box>}
    </div>
  );
}

function App() {
  const [tabValue, setTabValue] = useState(0);

  const handleTabChange = (_: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
  };

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
        <AppBar position="static">
          <Toolbar>
            <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
              DWSIM OTS - Instructor Station
            </Typography>
          </Toolbar>
          <Tabs value={tabValue} onChange={handleTabChange} sx={{ borderBottom: 1, borderColor: 'divider' }}>
            <Tab label="Session Monitor" />
            <Tab label="Scenario Editor" />
            <Tab label="Event Timeline" />
            <Tab label="Assessments" />
          </Tabs>
        </AppBar>

        <Container maxWidth={false} sx={{ flexGrow: 1 }}>
          <TabPanel value={tabValue} index={0}>
            <SessionMonitor />
          </TabPanel>
          <TabPanel value={tabValue} index={1}>
            <ScenarioEditor />
          </TabPanel>
          <TabPanel value={tabValue} index={2}>
            <EventTimeline />
          </TabPanel>
          <TabPanel value={tabValue} index={3}>
            <AssessmentViewer />
          </TabPanel>
        </Container>
      </Box>
    </ThemeProvider>
  );
}

export default App;
