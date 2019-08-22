// Copyright (c) Microsoft. All rights reserved.

import { withRouter } from 'react-router-dom';
import { connect } from 'react-redux';
import { withNamespaces } from 'react-i18next';

import { AuthService } from 'services';
import { getUser } from 'store/reducers/appReducer';
import { Profile } from './profile';

const mapStateToProps = state => ({
  user: getUser(state)
});

const mapDispatchToProps = () => ({
  logout: () => AuthService.logout()
});

// const mapTenantToProps = state => ({
//   tenantOptions: TenantService.AllTenants(getUser(state)),
//   currentTenant: TenantService.currentTenant(getUser(state))
// })

export const ProfileContainer = withRouter(withNamespaces()(connect(mapStateToProps, mapDispatchToProps)(Profile)));
