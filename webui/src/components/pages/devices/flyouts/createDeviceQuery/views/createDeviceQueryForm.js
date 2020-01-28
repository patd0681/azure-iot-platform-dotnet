// Copyright (c) Microsoft. All rights reserved.

import React from 'react';

import { permissions, toDiagnosticsModel } from 'services/models';
import { toConditionQueryModel } from 'services/models/iotHubManagerModels';
import { svgs, LinkedComponent, Validator } from 'utilities';
import {
  AjaxError,
  Btn,
  BtnToolbar,
  FormControl,
  FormGroup,
  FormLabel,
  Indicator,
  Protected
} from 'components/shared';
import { ConfigService } from 'services';
import {
  toCreateDeviceGroupRequestModel,
} from 'services/models';

import Flyout from 'components/shared/flyout';

const Section = Flyout.Section;

// A counter for creating unique keys per new condition
let conditionKey = 0;

const operators = ['EQ', 'GT', 'LT', 'GE', 'LE'];
const valueTypes = ['Number', 'Text'];

// Creates a state object for a condition
const newCondition = () => ({
  field: undefined,
  operator: undefined,
  type: undefined,
  value: '',
  key: conditionKey++ // Used by react to track the rendered elements
});

const toFormConditionModels = (models = []) => {
  return models.map(model => {
    return {
      field: model.key,
      operator: model.operator,
      value: model.value || '',
      type: model.value ? (isNaN(model.value) ? 'Text' : 'Number') : undefined,
      key: conditionKey++
    };
  });
};

class CreateDeviceQueryForm extends LinkedComponent {

  constructor(props) {
    super(props);

    this.state = {
      deviceQueryConditions: this.props.activeDeviceQueryConditions.length == 0 ? [newCondition()] : toFormConditionModels(this.props.activeDeviceQueryConditions),
      isPending: false,
      error: undefined,
    };

    // State to input links
    this.conditionsLink = this.linkTo('deviceQueryConditions');
    this.subscriptions = [];
  }

  conditionIsNew(condition) {
    return !condition.field && !condition.operator && !condition.type && !condition.value;
  }

  formIsValid() {
    return [
      this.conditionsLink
    ].every(link => !link.error);
  }

  componentWillUnmount() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  queryDevices = () => {
    return new Promise((resolve, reject) => {
      try {
        this.setState({ error: undefined, isPending: true }, () => {
          const rawQueryConditions = this.state.deviceQueryConditions.filter(condition => {
            // remove conditions that are new (have not been edited)
            return !this.conditionIsNew(condition)
          });
          this.props.setActiveDeviceQueryConditions(toConditionQueryModel(rawQueryConditions));
          this.props.fetchDevices()
          resolve();
        });
      }
      catch (error) {
        reject(error);
      }
    })
  }

  apply = (event) => {
    this.props.logEvent(toDiagnosticsModel('CreateDeviceQuery_Create', {}));
    event.preventDefault();
    this.queryDevices()
      .then(() => {
        this.setState({ error: undefined, isPending: false });
      })
      .catch(error => {
        this.setState({ error: error, isPending: false });
      });
  };

  addCondition = () => {
    this.props.logEvent(toDiagnosticsModel('CreateDeviceQuery_AddCondition', {}));
    return this.conditionsLink.set([
      ...this.conditionsLink.value,
      newCondition()
    ]);
  }

  deleteCondition = (index) =>
    () => {
      this.props.logEvent(toDiagnosticsModel('CreateDeviceQuery_RemoveCondition', {}));
      return this.conditionsLink.set(this.conditionsLink.value.filter((_, idx) => index !== idx));
    }

  resetFlyoutAndDevices = () => {
    return new Promise((resolve, reject) => {
      const resetConditions = [newCondition()];
      try {
        this.setState({ deviceQueryConditions: resetConditions, error: undefined, isPending: true }, () => {
          this.props.setActiveDeviceQueryConditions([]);
          this.props.fetchDevices();  // reload the devices grid with a blank query
          this.render();
          resolve();
        });
      }
      catch (error) {
        reject(error);
      }
    });
  }

  onReset = () => {
    this.props.logEvent(toDiagnosticsModel('CreateDeviceQuery_Reset', {}));
    this.resetFlyoutAndDevices()
      .then(() => {
        this.setState({ error: undefined, isPending: false });
      })
      .catch(error => {
        this.setState({ error: error, isPending: false });
      });;
  }

  createDeviceGroupFromQuery = () => {
    this.props.logEvent(toDiagnosticsModel('CreateDeviceQuery_CreateDeviceGroupFromQuery', {}));
    this.setState({ error: undefined, isPending: true });
    // Create the name based on the query
    const queryConditions = this.state.deviceQueryConditions.filter(condition => !this.conditionIsNew(condition));
    var deviceQueryConditionName = queryConditions
      .map(condition => condition.field + condition.operator + condition.value)
      .join(';');
    var deviceGroup = {
      displayName: this.props.activeDeviceGroup.displayName + ": " + (deviceQueryConditionName || 'All Devices'),
      conditions: queryConditions
    };
    this.subscriptions.push(
      ConfigService.createDeviceGroup(toCreateDeviceGroupRequestModel(deviceGroup))
        .subscribe(
          deviceGroup => {
            this.props.insertDeviceGroup(deviceGroup);
            this.setState({ error: undefined, isPending: false });
          },
          error => this.setState({ error, isPending: false }),
        )
    );
  }

  render() {
    const { t } = this.props;
    // Create the state link for the dynamic form elements
    const conditionLinks = this.conditionsLink.getLinkedChildren(conditionLink => {
      const field = conditionLink.forkTo('field')
        .map(({ value }) => value)
        .check(Validator.notEmpty, t('deviceQueryConditions.errorMsg.fieldCantBeEmpty'));
      const operator = conditionLink.forkTo('operator')
        .map(({ value }) => value)
        .check(Validator.notEmpty, t('deviceQueryConditions.errorMsg.operatorCantBeEmpty'));
      const type = conditionLink.forkTo('type')
        .map(({ value }) => value)
        .check(Validator.notEmpty, t('deviceQueryConditions.errorMsg.typeCantBeEmpty'));
      const value = conditionLink.forkTo('value')
        .check(Validator.notEmpty, t('deviceQueryConditions.errorMsg.valueCantBeEmpty'))
        .check(val => type.value === 'Number' ? !isNaN(val) : true, t('deviceQueryConditions.errorMsg.selectedType'));
      const edited = !(!field.value && !operator.value && !value.value && !type.value);
      var error = (edited && (field.error || operator.error || value.error || type.error)) || '';
      return { field, operator, value, type, edited, error };
    });

    const editedConditions = conditionLinks.filter(({ edited }) => edited);
    const conditionHasErrors = editedConditions.some(({ error }) => !!error);

    const operatorOptions = operators.map(value => ({
      label: t(`deviceQueryConditions.operatorOptions.${value}`),
      value
    }));
    const typeOptions = valueTypes.map(value => ({
      label: t(`deviceQueryConditions.typeOptions.${value}`),
      value
    }));

    return (
      <form onSubmit={this.apply}>
        <Section.Container collapsable={false} className="borderless">
          <Btn className="add-btn" svg={svgs.plus} onClick={this.addCondition}>
            {t(`deviceQueryConditions.add`)}
          </Btn>
          <Section.Content>
            {
              conditionLinks.map((condition, idx) => (
                <Section.Container key={this.state.deviceQueryConditions[idx].key}>
                  <Section.Header>
                    {t('deviceQueryConditions.condition', { headerCount: idx + 1 })}
                  </Section.Header>
                  <Section.Content>
                    <FormGroup>
                      <FormLabel isRequired="true">
                        {t('deviceQueryConditions.field')}
                      </FormLabel>
                      {
                        this.props.filtersError
                          ? <AjaxError t={t} error={this.props.filtersError} />
                          : <FormControl
                            type="select"
                            ariaLabel={t('deviceQueryConditions.field')}
                            className="long"
                            searchable={false}
                            clearable={false}
                            placeholder={t('deviceQueryConditions.fieldPlaceholder')}
                            options={this.props.filterOptions}
                            link={condition.field} />
                      }
                    </FormGroup>
                    <FormGroup>
                      <FormLabel isRequired="true">
                        {t('deviceQueryConditions.operator')}
                      </FormLabel>
                      <FormControl
                        type="select"
                        ariaLabel={t('deviceQueryConditions.operator')}
                        className="long"
                        searchable={false}
                        clearable={false}
                        options={operatorOptions}
                        placeholder={t('deviceQueryConditions.operatorPlaceholder')}
                        link={condition.operator} />
                    </FormGroup>
                    <FormGroup>
                      <FormLabel isRequired="true">
                        {t('deviceQueryConditions.value')}
                      </FormLabel>
                      <FormControl
                        type="text"
                        placeholder={t('deviceQueryConditions.valuePlaceholder')}
                        link={condition.value} />
                    </FormGroup>
                    <FormGroup>
                      <FormLabel isRequired="true">
                        {t('deviceQueryConditions.type')}
                      </FormLabel>
                      <FormControl
                        type="select"
                        ariaLabel={t('deviceQueryConditions.type')}
                        className="short"
                        clearable={false}
                        searchable={false}
                        options={typeOptions}
                        placeholder={t('deviceQueryConditions.typePlaceholder')}
                        link={condition.type} />
                    </FormGroup>
                    <BtnToolbar>
                      <Btn onClick={this.deleteCondition(idx)}>{t('deviceQueryConditions.remove')}</Btn>
                    </BtnToolbar>
                  </Section.Content>
                </Section.Container>
              ))
            }
            {this.state.isPending && <Indicator pattern="bar" size="medium" />}
            <BtnToolbar>
              <Btn
                primary
                disabled={!this.formIsValid() || conditionHasErrors || this.state.isPending}
                type="submit">
                {t('createDeviceQueryFlyout.create')}
              </Btn>
              <Protected permission={permissions.deleteDeviceGroups}>
                <Btn
                  disabled={!this.formIsValid() || conditionHasErrors || this.state.isPending}
                  svg={svgs.plus}
                  onClick={this.createDeviceGroupFromQuery}>
                  {t('createDeviceQueryFlyout.createDeviceGroup')}
                </Btn>
              </Protected>
              <Btn
                disabled={this.state.isPending}
                svg={svgs.cancelX}
                onClick={this.onReset}>
                {t('createDeviceQueryFlyout.reset')}
              </Btn>
            </BtnToolbar>
            {this.state.error && <AjaxError t={t} error={this.state.error} />}
          </Section.Content>
        </Section.Container>
      </form>
    );
  }
}

export default CreateDeviceQueryForm;
