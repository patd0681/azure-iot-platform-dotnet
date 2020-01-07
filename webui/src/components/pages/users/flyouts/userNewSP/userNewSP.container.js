// Copyright (c) Microsoft. All rights reserved.

import { connect } from 'react-redux';
import { withNamespaces } from 'react-i18next';
import { UserNewSP } from './userNewSP';
import {
  epics as usersEpics,
  redux as usersRedux
} from 'store/reducers/usersReducer';
import {
  epics as appEpics,
} from 'store/reducers/appReducer';

// Pass the global info needed
const mapStateToProps = state => ({
});

// Wrap the dispatch method
const mapDispatchToProps = dispatch => ({
  insertUsers: users => dispatch(usersRedux.actions.insertUsers(users)),
  fetchUsers: () => dispatch(usersEpics.actions.fetchUsers()),
  logEvent: diagnosticsModel => dispatch(appEpics.actions.logEvent(diagnosticsModel))
});

export const UserNewSPContainer = withNamespaces()(connect(mapStateToProps, mapDispatchToProps)(UserNewSP));
